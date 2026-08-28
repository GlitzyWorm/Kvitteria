// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MobileApp.Features.Auth;

namespace MobileApp.Services;

/// <summary>Either a session, or the resource key naming why there isn't one.</summary>
public sealed record AuthOutcome(StoredSession? Session, string? ErrorResourceKey, string? ServerMessage = null)
{
	public bool Succeeded => Session is not null;

	public static AuthOutcome Success(StoredSession session) => new(session, null);

	public static AuthOutcome Failure(string errorResourceKey, string? serverMessage = null)
		=> new(null, errorResourceKey, serverMessage);
}

/// <summary>
/// Talks to GoTrue, which owns identity. This app never validates a token and never keeps a
/// password — it exchanges one for a session and forgets it.
/// </summary>
/// <remarks>
/// Uses <see cref="HttpClients.Anonymous"/> on purpose. Routing a refresh through the client that
/// refreshes on 401 would recurse.
/// </remarks>
public sealed class GoTrueClient(
	IHttpClientFactory httpClientFactory,
	IServerSettings serverSettings,
	ILogger<GoTrueClient> logger)
{
	/// <summary>
	/// Caddy strips this prefix and GoTrue serves at its root; in a Supabase project Kong is what
	/// adds it. The one place the path is written down.
	/// </summary>
	private const string AuthPrefix = "/auth/v1";

	public Task<AuthOutcome> SignInAsync(string email, string password, CancellationToken ct)
		=> PostAsync(
			$"{AuthPrefix}/token?grant_type=password",
			JsonContent.Create(new PasswordGrantRequest(email, password), KvitteriaJsonContext.Default.PasswordGrantRequest),
			email,
			ct);

	public Task<AuthOutcome> SignUpAsync(string email, string password, CancellationToken ct)
		=> PostAsync(
			$"{AuthPrefix}/signup",
			JsonContent.Create(new PasswordGrantRequest(email, password), KvitteriaJsonContext.Default.PasswordGrantRequest),
			email,
			ct);

	public Task<AuthOutcome> RefreshAsync(StoredSession expiring, CancellationToken ct)
		=> PostAsync(
			$"{AuthPrefix}/token?grant_type=refresh_token",
			JsonContent.Create(new RefreshGrantRequest(expiring.RefreshToken), KvitteriaJsonContext.Default.RefreshGrantRequest),
			expiring.Email,
			ct);

	/// <summary>
	/// Best effort. The local session is cleared whatever happens here: someone who taps sign out is
	/// signed out even with the server unreachable, and an orphaned server-side session expires on
	/// its own within the hour.
	/// </summary>
	public async Task SignOutAsync(StoredSession session, CancellationToken ct)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, serverSettings.Resolve($"{AuthPrefix}/logout"));
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

			using HttpResponseMessage response = await httpClientFactory
				.CreateClient(HttpClients.Anonymous)
				.SendAsync(request, ct);
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogDebug(exception, "Sign-out request failed; clearing the local session anyway.");
		}
	}

	private async Task<AuthOutcome> PostAsync(string route, HttpContent payload, string email, CancellationToken ct)
	{
		HttpResponseMessage response;

		try
		{
			using (payload)
			{
				response = await httpClientFactory
					.CreateClient(HttpClients.Anonymous)
					.PostAsync(serverSettings.Resolve(route), payload, ct);
			}
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogDebug(exception, "Could not reach GoTrue at {Route}.", route);
			return AuthOutcome.Failure(AuthErrorMapper.NetworkKey);
		}

		using (response)
		{
			string body = await response.Content.ReadAsStringAsync(ct);

			if (!response.IsSuccessStatusCode)
			{
				string key = AuthErrorMapper.MapResourceKey((int)response.StatusCode, body, out string? serverMessage);
				logger.LogDebug("GoTrue rejected {Route} with {Status}: {Message}", route, (int)response.StatusCode, serverMessage);
				return AuthOutcome.Failure(key, serverMessage);
			}

			GoTrueSession? session;

			try
			{
				session = JsonSerializer.Deserialize(body, KvitteriaJsonContext.Default.GoTrueSession);
			}
			catch (JsonException exception)
			{
				logger.LogDebug(exception, "GoTrue answered {Route} with something that is not a session.", route);
				return AuthOutcome.Failure(AuthErrorMapper.GenericKey);
			}

			if (session is null || !session.HasTokens)
			{
				// A 200 with no tokens means confirmation is required. The shipped compose sets
				// GOTRUE_MAILER_AUTOCONFIRM and this project configures no SMTP at all, so a
				// self-hoster who turned that off has made an account nobody can ever confirm.
				return AuthOutcome.Failure("AuthErrorConfirmationRequired");
			}

			return AuthOutcome.Success(StoredSession.FromTokenResponse(
				session.AccessToken,
				session.RefreshToken,
				session.ExpiresAt,
				session.ExpiresIn,
				session.User?.Email ?? email,
				DateTimeOffset.UtcNow));
		}
	}
}
