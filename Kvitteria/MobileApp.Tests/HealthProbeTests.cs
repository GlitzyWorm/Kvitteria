// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Auth;

namespace MobileApp.Tests;

/// <summary>
/// Reading the household state out of a readiness report. The bodies here are the shape
/// <c>HealthRegistration.WriteReportAsync</c> writes, including the case that matters most: an
/// unhealthy report, which arrives with a 503 and still answers this question perfectly well.
/// </summary>
public class HealthProbeTests
{
	private const string HouseholdDegraded = """
		{
		  "status": "Degraded",
		  "totalDurationMs": 42,
		  "checks": [
		    { "name": "postgres", "status": "Healthy", "durationMs": 3 },
		    { "name": "migrations", "status": "Healthy", "durationMs": 1 },
		    { "name": "ollama", "status": "Healthy", "durationMs": 8 },
		    { "name": "ollama-model", "status": "Healthy", "durationMs": 9 },
		    { "name": "household", "status": "Degraded", "durationMs": 2,
		      "description": "No active admin yet; the first person to sign in claims the household." }
		  ]
		}
		""";

	private const string HouseholdHealthy = """
		{
		  "status": "Healthy",
		  "checks": [
		    { "name": "postgres", "status": "Healthy" },
		    { "name": "household", "status": "Healthy" }
		  ]
		}
		""";

	[Fact]
	public void Degraded_household_means_unclaimed()
		=> Assert.Equal(HouseholdState.Unclaimed, HealthProbe.ReadHouseholdState(HouseholdDegraded));

	[Fact]
	public void Healthy_household_means_claimed()
		=> Assert.Equal(HouseholdState.Claimed, HealthProbe.ReadHouseholdState(HouseholdHealthy));

	/// <summary>
	/// The reason this reads a body rather than a status code. An unpulled Ollama model makes the
	/// whole report unhealthy and the endpoint answer 503; the household check beside it is still
	/// authoritative, and a fresh install must not be sent to the wrong screen because of it.
	/// </summary>
	[Fact]
	public void Reads_the_household_check_out_of_an_unhealthy_report()
	{
		const string body = """
			{
			  "status": "Unhealthy",
			  "checks": [
			    { "name": "ollama-model", "status": "Unhealthy", "description": "Model not pulled" },
			    { "name": "household", "status": "Degraded" }
			  ]
			}
			""";

		Assert.Equal(HouseholdState.Unclaimed, HealthProbe.ReadHouseholdState(body));
	}

	/// <summary>An unhealthy household check means the database is unreachable, not that it is empty.</summary>
	[Fact]
	public void Unhealthy_household_says_nothing_either_way()
	{
		const string body = """{ "checks": [ { "name": "household", "status": "Unhealthy" } ] }""";

		Assert.Equal(HouseholdState.Unknown, HealthProbe.ReadHouseholdState(body));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("<html><body>404</body></html>")]
	[InlineData("{ }")]
	[InlineData("""{ "checks": "not an array" }""")]
	[InlineData("""{ "checks": [ { "name": "postgres", "status": "Healthy" } ] }""")]
	[InlineData("""{ "checks": [ { "name": "household" } ] }""")]
	public void Anything_else_is_unknown(string? body)
		=> Assert.Equal(HouseholdState.Unknown, HealthProbe.ReadHouseholdState(body));
}
