// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;

namespace MobileApp.Features.Auth;

/// <summary>
/// What went wrong at the identity provider, named in this app's terms. The values are resource
/// keys in <c>Localization/AppResources.resx</c>, so a mapped failure reaches the screen in the
/// user's language rather than as GoTrue's English prose.
/// </summary>
public static class AuthErrorMapper
{
	/// <summary>Shown for anything unrecognized. The server's own wording goes to the debug log instead.</summary>
	public const string GenericKey = "AuthErrorGeneric";

	/// <summary>Neither a response nor a rejection — the request never completed.</summary>
	public const string NetworkKey = "AuthErrorNetwork";

	private static readonly Dictionary<string, string> KeysByErrorCode = new(StringComparer.OrdinalIgnoreCase)
	{
		["invalid_credentials"] = "AuthErrorInvalidCredentials",
		["invalid_grant"] = "AuthErrorInvalidCredentials",
		["user_already_exists"] = "AuthErrorUserAlreadyExists",
		["email_exists"] = "AuthErrorUserAlreadyExists",
		["weak_password"] = "AuthErrorWeakPassword",
		["email_address_invalid"] = "AuthErrorEmailInvalid",
		["validation_failed"] = "AuthErrorEmailInvalid",
		["email_not_confirmed"] = "AuthErrorEmailNotConfirmed",
		["over_request_rate_limit"] = "AuthErrorRateLimited",
		["over_email_send_rate_limit"] = "AuthErrorRateLimited",
		["signup_disabled"] = "AuthErrorSignupDisabled",
		["refresh_token_not_found"] = "AuthErrorSessionExpired",
		["refresh_token_already_used"] = "AuthErrorSessionExpired"
	};

	/// <summary>
	/// Maps a GoTrue error response onto a resource key. Both response shapes the service emits are
	/// handled: the current <c>{code, error_code, msg}</c> and the OAuth-style
	/// <c>{error, error_description}</c> that the token endpoint still uses for some rejections.
	/// </summary>
	/// <param name="statusCode">The HTTP status, used only when the body names no code.</param>
	/// <param name="body">The raw response body, which may be empty or not be JSON at all.</param>
	/// <param name="serverMessage">
	/// The server's own wording, for the debug log. Never shown: it is English, and it names GoTrue.
	/// </param>
	public static string MapResourceKey(int statusCode, string? body, out string? serverMessage)
	{
		serverMessage = null;

		string? errorCode = null;

		if (!string.IsNullOrWhiteSpace(body))
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(body);
				JsonElement root = document.RootElement;

				if (root.ValueKind == JsonValueKind.Object)
				{
					errorCode = ReadString(root, "error_code") ?? ReadString(root, "error");
					serverMessage = ReadString(root, "msg")
					                ?? ReadString(root, "error_description")
					                ?? ReadString(root, "message");
				}
			}
			catch (JsonException)
			{
				// A proxy or a wrong origin answering with HTML. The status code still says something.
				serverMessage = body.Length > 200 ? body[..200] : body;
			}
		}

		if (errorCode is not null && KeysByErrorCode.TryGetValue(errorCode, out string? mapped))
		{
			return mapped;
		}

		// No code, or one this app has not seen. The status still separates "you got it wrong" from
		// "the server is unwell", which are different sentences to a user.
		return statusCode switch
		{
			400 or 401 => "AuthErrorInvalidCredentials",
			422 => "AuthErrorEmailInvalid",
			429 => "AuthErrorRateLimited",
			>= 500 => "AuthErrorServer",
			_ => GenericKey
		};
	}

	private static string? ReadString(JsonElement element, string propertyName)
		=> element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
}
