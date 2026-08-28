// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net;
using System.Net.Http.Headers;
using MobileApp.Features.Auth;

namespace MobileApp.Services;

/// <summary>
/// Attaches the bearer to every <c>/api</c> request and keeps it current. Sits on the
/// <see cref="HttpClients.Api"/> client only — GoTrue's own client must not have it, or a refresh
/// would refresh.
/// </summary>
public sealed class AuthenticatedHttpMessageHandler(ISessionService sessions) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
	{
		StoredSession? session = await sessions.EnsureFreshAsync(ct);

		if (session is null)
		{
			// No session, or a refresh that failed. Let the request go out bare: the backend answers
			// 401, which is exactly what the caller must handle anyway, and inventing a response here
			// would hide a real server error behind a synthetic one.
			return await base.SendAsync(request, ct);
		}

		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

		HttpResponseMessage response = await base.SendAsync(request, ct);

		if (response.StatusCode != HttpStatusCode.Unauthorized)
		{
			return response;
		}

		// The token was fresh by the clock and rejected anyway — a restarted GoTrue, a rotated key,
		// or a clock that disagrees. Worth exactly one retry; a loop here would hammer the server
		// with a credential it has already refused.
		StoredSession? refreshed = await sessions.RefreshAsync(session.AccessToken, ct);

		if (refreshed is null || string.Equals(refreshed.AccessToken, session.AccessToken, StringComparison.Ordinal))
		{
			return response;
		}

		response.Dispose();

		using HttpRequestMessage retry = await CloneAsync(request, ct);
		retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);

		return await base.SendAsync(retry, ct);
	}

	/// <summary>
	/// A sent <see cref="HttpRequestMessage"/> cannot be sent twice, and its content stream has
	/// already been read, so the retry needs a copy with the body buffered.
	/// </summary>
	private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original, CancellationToken ct)
	{
		var clone = new HttpRequestMessage(original.Method, original.RequestUri)
		{
			Version = original.Version,
			VersionPolicy = original.VersionPolicy
		};

		foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
		{
			clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		foreach (KeyValuePair<string, object?> option in original.Options)
		{
			clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
		}

		if (original.Content is not null)
		{
			byte[] body = await original.Content.ReadAsByteArrayAsync(ct);
			var content = new ByteArrayContent(body);

			foreach (KeyValuePair<string, IEnumerable<string>> header in original.Content.Headers)
			{
				content.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			clone.Content = content;
		}

		return clone;
	}
}
