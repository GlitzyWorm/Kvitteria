// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;

namespace MobileApp.Features.Auth;

/// <summary>Whether a server already has an active admin, as far as an anonymous caller can tell.</summary>
public enum HouseholdState
{
	/// <summary>The report did not name the household check, or could not be read at all.</summary>
	Unknown,

	/// <summary>No active admin yet. The first account to sign in claims the household.</summary>
	Unclaimed,

	/// <summary>An admin exists; a new account needs to have been pre-authorized.</summary>
	Claimed
}

/// <summary>
/// Reads <c>GET /health/ready</c>. There is no dedicated "is this server claimed" endpoint — the
/// <c>household</c> check inside the readiness report is the only signal available before a token
/// exists, and it reports <c>Degraded</c> exactly while no active admin does.
/// Free of MAUI types so <c>MobileApp.Tests</c> can link it as source.
/// </summary>
public static class HealthProbe
{
	/// <summary>
	/// Parses the readiness report body. The caller must hand this the body <em>whatever</em> the
	/// status code was: the health middleware answers 503 for an unhealthy report, and a cold Ollama
	/// is unhealthy while the household question is perfectly answerable. Reading only 2xx bodies
	/// would strand a fresh install on the wrong screen for a reason that has nothing to do with it.
	/// </summary>
	public static HouseholdState ReadHouseholdState(string? body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return HouseholdState.Unknown;
		}

		try
		{
			using JsonDocument document = JsonDocument.Parse(body);

			if (!document.RootElement.TryGetProperty("checks", out JsonElement checks)
			    || checks.ValueKind != JsonValueKind.Array)
			{
				return HouseholdState.Unknown;
			}

			foreach (JsonElement check in checks.EnumerateArray())
			{
				if (check.ValueKind != JsonValueKind.Object
				    || !check.TryGetProperty("name", out JsonElement name)
				    || name.ValueKind != JsonValueKind.String
				    || !string.Equals(name.GetString(), "household", StringComparison.Ordinal))
				{
					continue;
				}

				if (!check.TryGetProperty("status", out JsonElement status) || status.ValueKind != JsonValueKind.String)
				{
					return HouseholdState.Unknown;
				}

				return status.GetString() switch
				{
					"Degraded" => HouseholdState.Unclaimed,
					"Healthy" => HouseholdState.Claimed,

					// Unhealthy means the check itself failed — most likely the database is down, in
					// which case nothing is known about the household either way.
					_ => HouseholdState.Unknown
				};
			}

			return HouseholdState.Unknown;
		}
		catch (JsonException)
		{
			// Something answered on that origin, but not with a readiness report. Whether it is a
			// Kvitteria server at all is /version's question, not this one's.
			return HouseholdState.Unknown;
		}
	}
}
