// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kvitteria.Contracts.DTO;
using Microsoft.Extensions.Logging;
using MobileApp.Features.Auth;

namespace MobileApp.Services;

/// <summary>What <c>GET /api/users/me</c> said about this account.</summary>
public enum MembershipResult
{
	/// <summary>An active household member. <c>User</c> is set.</summary>
	Member,

	/// <summary>
	/// Authenticated, but not a member — or a disabled one. The backend answers 403 rather than 401
	/// here, which is why this is a distinct screen and not a failed sign-in.
	/// </summary>
	NotAMember,

	/// <summary>The token was refused. Whatever is stored is worthless; sign in again.</summary>
	Unauthenticated,

	/// <summary>The server never answered. Says nothing about the account.</summary>
	Unreachable
}

public sealed record MembershipOutcome(MembershipResult Result, CurrentUserDto? User);

/// <summary>Talks to the backend, which owns authorization.</summary>
public sealed class KvitteriaApiClient(
	IHttpClientFactory httpClientFactory,
	IServerSettings serverSettings,
	ILogger<KvitteriaApiClient> logger)
{
	/// <summary>
	/// Proves the origin is a Kvitteria backend rather than merely something that answers. Anonymous,
	/// cheap, and it fails fast — which is what the server-URL screen needs from a typo.
	/// </summary>
	public async Task<VersionDto?> GetVersionAsync(Uri origin, CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await httpClientFactory
				.CreateClient(HttpClients.Anonymous)
				.GetAsync(new Uri(origin, "/version"), ct);

			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			string body = await response.Content.ReadAsStringAsync(ct);
			return JsonSerializer.Deserialize(body, KvitteriaJsonContext.Default.VersionDto);
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
		{
			logger.LogDebug(exception, "No Kvitteria backend answered at {Origin}.", origin);
			return null;
		}
	}

	/// <summary>
	/// Whether anything at all answers HTTP on that origin. Only ever used to tell "nothing is
	/// there" apart from "something is there, but it is not Kvitteria" — two different mistakes on
	/// the server screen, needing two different sentences. Any status counts, 404 included.
	/// </summary>
	public async Task<bool> RespondsAsync(Uri origin, CancellationToken ct)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Head, origin);

			using HttpResponseMessage response = await httpClientFactory
				.CreateClient(HttpClients.Anonymous)
				.SendAsync(request, ct);

			return true;
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogDebug(exception, "Nothing answered at {Origin}.", origin);
			return false;
		}
	}

	/// <summary>
	/// Asks whether the household has been claimed. There is no endpoint for this question — the
	/// <c>household</c> check inside the readiness report is the only anonymous signal, and it
	/// reports Degraded exactly while no active admin exists.
	/// </summary>
	public async Task<HouseholdState> GetHouseholdStateAsync(Uri origin, CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await httpClientFactory
				.CreateClient(HttpClients.Anonymous)
				.GetAsync(new Uri(origin, "/health/ready"), ct);

			// Read the body whatever the status was. The health middleware answers 503 for an
			// unhealthy report, and a cold Ollama model makes the whole report unhealthy while the
			// household question stays perfectly answerable.
			string body = await response.Content.ReadAsStringAsync(ct);
			return HealthProbe.ReadHouseholdState(body);
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogDebug(exception, "Readiness probe failed at {Origin}.", origin);
			return HouseholdState.Unknown;
		}
	}

	/// <summary>
	/// Resolves the caller against the household. On a household with no members at all, this call
	/// is what bootstraps the caller into its admin — there is no claim endpoint, and
	/// <c>UserProvisioning.Decide</c> settles it as a side effect of the first authenticated request.
	/// </summary>
	public async Task<MembershipOutcome> GetCurrentUserAsync(CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await httpClientFactory
				.CreateClient(HttpClients.Api)
				.GetAsync(serverSettings.Resolve("/api/users/me"), ct);

			if (response.StatusCode is HttpStatusCode.Forbidden)
			{
				return new MembershipOutcome(MembershipResult.NotAMember, null);
			}

			if (response.StatusCode is HttpStatusCode.Unauthorized)
			{
				return new MembershipOutcome(MembershipResult.Unauthenticated, null);
			}

			if (!response.IsSuccessStatusCode)
			{
				logger.LogDebug("GET /api/users/me answered {Status}.", (int)response.StatusCode);
				return new MembershipOutcome(MembershipResult.Unreachable, null);
			}

			string body = await response.Content.ReadAsStringAsync(ct);
			CurrentUserDto? user = JsonSerializer.Deserialize(body, KvitteriaJsonContext.Default.CurrentUserDto);

			return user is null
				? new MembershipOutcome(MembershipResult.Unreachable, null)
				: new MembershipOutcome(MembershipResult.Member, user);
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
		{
			logger.LogDebug(exception, "Could not resolve the current user.");
			return new MembershipOutcome(MembershipResult.Unreachable, null);
		}
	}
}
