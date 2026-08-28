// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json.Serialization;

namespace MobileApp.Features.Auth;

/// <summary>
/// GoTrue's token response. Deliberately <em>not</em> in <c>Contracts</c>: that project holds the
/// wire types this application defines, and this one belongs to the identity provider. Moving it
/// there would also make the backend compile against a shape it never sees — it validates tokens,
/// it never issues them.
/// </summary>
public sealed record GoTrueSession
{
	[JsonPropertyName("access_token")]
	public string AccessToken { get; init; } = string.Empty;

	[JsonPropertyName("refresh_token")]
	public string RefreshToken { get; init; } = string.Empty;

	[JsonPropertyName("token_type")]
	public string? TokenType { get; init; }

	/// <summary>Seconds from now. Present on every response; <see cref="ExpiresAt"/> is not.</summary>
	[JsonPropertyName("expires_in")]
	public long? ExpiresIn { get; init; }

	/// <summary>Unix seconds, from the server's clock. Preferred when present.</summary>
	[JsonPropertyName("expires_at")]
	public long? ExpiresAt { get; init; }

	[JsonPropertyName("user")]
	public GoTrueUser? User { get; init; }

	/// <summary>
	/// True when the response carries a usable session. A signup answers 200 with the user and no
	/// tokens when confirmation mail is required — the shipped compose sets
	/// <c>GOTRUE_MAILER_AUTOCONFIRM=true</c> so that does not happen here, but a self-hoster who
	/// turns it off should see a sentence rather than an empty bearer header.
	/// </summary>
	public bool HasTokens => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(RefreshToken);
}

public sealed record GoTrueUser
{
	[JsonPropertyName("id")]
	public string? Id { get; init; }

	[JsonPropertyName("email")]
	public string? Email { get; init; }
}
