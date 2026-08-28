// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Auth;

/// <summary>
/// The part of a GoTrue session worth keeping between launches. Free of MAUI types so
/// <c>MobileApp.Tests</c> can link it as source; where it is kept is <c>ISessionStore</c>'s problem.
/// </summary>
/// <param name="AccessToken">The bearer token sent to <c>/api/*</c>.</param>
/// <param name="RefreshToken">
/// Single-use. GoTrue rotates on every refresh with only a ten-second reuse interval, so two
/// concurrent refreshes spend the session — see <c>AuthenticatedHttpMessageHandler</c>.
/// </param>
/// <param name="ExpiresAt">When <paramref name="AccessToken"/> stops being accepted.</param>
/// <param name="Email">The address the session belongs to, so the "no access" screen can name it.</param>
public sealed record StoredSession(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, string Email)
{
	/// <summary>
	/// How early a token counts as spent. A request that starts inside this window would very likely
	/// arrive after expiry, and a proactive refresh is cheaper than a 401 and a retry.
	/// </summary>
	public static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

	public bool IsExpiring(DateTimeOffset now) => IsExpiring(now, RefreshSkew);

	public bool IsExpiring(DateTimeOffset now, TimeSpan skew) => ExpiresAt - now <= skew;

	/// <summary>
	/// Builds a session from a GoTrue token response.
	/// <paramref name="expiresAt"/> is preferred over <paramref name="expiresIn"/> because it is an
	/// absolute instant from the server's clock; the relative form is only correct if the response
	/// is stamped the moment it is read, and is the fallback for the responses that omit the other.
	/// </summary>
	public static StoredSession FromTokenResponse(
		string accessToken,
		string refreshToken,
		long? expiresAt,
		long? expiresIn,
		string email,
		DateTimeOffset now)
	{
		DateTimeOffset resolved = expiresAt is { } absolute
			? DateTimeOffset.FromUnixTimeSeconds(absolute)
			: now.AddSeconds(expiresIn ?? 0);

		return new StoredSession(accessToken, refreshToken, resolved, email);
	}
}
