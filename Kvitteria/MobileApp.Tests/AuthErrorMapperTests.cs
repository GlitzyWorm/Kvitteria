// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Auth;

namespace MobileApp.Tests;

/// <summary>
/// Turning GoTrue's English rejections into a resource key. The bodies are the two shapes the
/// service actually emits: the current one, and the OAuth-style one the token endpoint still uses
/// for some grants.
/// </summary>
public class AuthErrorMapperTests
{
	[Theory]
	[InlineData("invalid_credentials", "AuthErrorInvalidCredentials")]
	[InlineData("user_already_exists", "AuthErrorUserAlreadyExists")]
	[InlineData("email_exists", "AuthErrorUserAlreadyExists")]
	[InlineData("weak_password", "AuthErrorWeakPassword")]
	[InlineData("email_address_invalid", "AuthErrorEmailInvalid")]
	[InlineData("email_not_confirmed", "AuthErrorEmailNotConfirmed")]
	[InlineData("over_request_rate_limit", "AuthErrorRateLimited")]
	[InlineData("over_email_send_rate_limit", "AuthErrorRateLimited")]
	[InlineData("signup_disabled", "AuthErrorSignupDisabled")]
	[InlineData("refresh_token_already_used", "AuthErrorSessionExpired")]
	public void Maps_a_known_error_code(string errorCode, string expectedKey)
	{
		string body = $$"""{ "code": 400, "error_code": "{{errorCode}}", "msg": "whatever" }""";

		Assert.Equal(expectedKey, AuthErrorMapper.MapResourceKey(400, body, out _));
	}

	/// <summary>The token endpoint's older shape. Same rejection, different field names.</summary>
	[Fact]
	public void Maps_the_oauth_style_shape()
	{
		const string body = """{ "error": "invalid_grant", "error_description": "Invalid login credentials" }""";

		Assert.Equal("AuthErrorInvalidCredentials", AuthErrorMapper.MapResourceKey(400, body, out string? message));
		Assert.Equal("Invalid login credentials", message);
	}

	/// <summary>
	/// A code this app has not seen still has a status behind it, and 400 from a sign-in means the
	/// credentials were wrong — a better sentence than the generic one.
	/// </summary>
	[Theory]
	[InlineData(400, "AuthErrorInvalidCredentials")]
	[InlineData(401, "AuthErrorInvalidCredentials")]
	[InlineData(422, "AuthErrorEmailInvalid")]
	[InlineData(429, "AuthErrorRateLimited")]
	[InlineData(500, "AuthErrorServer")]
	[InlineData(503, "AuthErrorServer")]
	[InlineData(418, "AuthErrorGeneric")]
	public void Falls_back_to_the_status(int status, string expectedKey)
	{
		const string body = """{ "code": 0, "error_code": "something_new", "msg": "?" }""";

		Assert.Equal(expectedKey, AuthErrorMapper.MapResourceKey(status, body, out _));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Survives_an_empty_body(string? body)
	{
		Assert.Equal("AuthErrorInvalidCredentials", AuthErrorMapper.MapResourceKey(400, body, out string? message));
		Assert.Null(message);
	}

	/// <summary>
	/// A proxy answering with HTML instead of the service. The status is still meaningful, and the
	/// body is kept for the debug log rather than thrown away.
	/// </summary>
	[Fact]
	public void Survives_a_body_that_is_not_json()
	{
		const string body = "<html><head><title>502 Bad Gateway</title></head></html>";

		Assert.Equal("AuthErrorServer", AuthErrorMapper.MapResourceKey(502, body, out string? message));
		Assert.Equal(body, message);
	}

	/// <summary>A very long non-JSON body is truncated: this only ever reaches a log line.</summary>
	[Fact]
	public void Truncates_a_long_non_json_body()
	{
		string body = new('x', 500);

		AuthErrorMapper.MapResourceKey(500, body, out string? message);

		Assert.Equal(200, message?.Length);
	}

	/// <summary>
	/// The server's own wording is never returned as the key. It is English, and it names GoTrue —
	/// neither belongs on a Danish screen in an app that presents the server as "your server".
	/// </summary>
	[Fact]
	public void Never_returns_the_server_message_as_the_key()
	{
		const string body = """{ "code": 400, "error_code": "invalid_credentials", "msg": "Invalid login credentials" }""";

		string key = AuthErrorMapper.MapResourceKey(400, body, out string? message);

		Assert.NotEqual(message, key);
		Assert.Equal("Invalid login credentials", message);
	}

	[Fact]
	public void Error_codes_match_case_insensitively()
		=> Assert.Equal(
			"AuthErrorWeakPassword",
			AuthErrorMapper.MapResourceKey(400, """{ "error_code": "Weak_Password" }""", out _));
}
