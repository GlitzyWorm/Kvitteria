// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;

namespace MobileApp.Features.Shared;

/// <summary>
/// The one field of an RFC 7807 response this app shows anyone. The backend puts its whole human
/// message in <c>detail</c> — never in <c>title</c>, and never as an <c>errors</c> dictionary — so
/// reading that one string is the entire contract.
/// </summary>
public static class ProblemDetail
{
	/// <summary>
	/// The <c>detail</c> of a problem+json body, or null if there isn't one. Null in, null out, and a
	/// body that is not JSON at all is null rather than an exception: an error path is the worst place
	/// to throw a second error, and a proxy answering 502 with HTML is a real thing that happens.
	/// </summary>
	public static string? Read(string? body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return null;
		}

		try
		{
			using JsonDocument document = JsonDocument.Parse(body);

			if (document.RootElement.ValueKind is not JsonValueKind.Object ||
			    !document.RootElement.TryGetProperty("detail", out JsonElement detail) ||
			    detail.ValueKind is not JsonValueKind.String)
			{
				return null;
			}

			string? value = detail.GetString();

			// An empty detail is the same as an absent one to a caller deciding whether to show it.
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
