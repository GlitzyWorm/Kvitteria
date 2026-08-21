using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace Backend.Features.Health;

public static class HealthRegistration
{
	/// <summary>Checks carrying this tag make up readiness; liveness runs none of them.</summary>
	private const string ReadyTag = "ready";

	extension(IServiceCollection services)
	{
		/// <summary>
		/// Registers one named check per dependency, rather than a single pass/fail, so a broken install
		/// says which piece is broken. Every check resolves its own scoped dependencies from the scope the
		/// health service creates per probe.
		/// </summary>
		public IServiceCollection AddHouseholdHealthChecks()
		{
			services.AddHealthChecks()
			        .AddCheck<PostgresHealthCheck>("postgres", tags:[ReadyTag])
			        .AddCheck<MigrationsHealthCheck>("migrations", tags:[ReadyTag])
			        .AddCheck<OllamaHealthCheck>("ollama", tags:[ReadyTag])
			        .AddCheck<OllamaModelHealthCheck>("ollama-model", tags:[ReadyTag])
			        .AddCheck<HouseholdHealthCheck>("household", tags:[ReadyTag]);

			return services;
		}
	}

	extension(WebApplication app)
	{
		/// <summary>
		/// Maps both probes outside the <c>/api</c> group, which requires an active household member — a
		/// probe that needs a bearer token cannot tell a container orchestrator anything.
		/// </summary>
		public void MapHouseholdHealthChecks()
		{
			bool includeExceptionDetail = app.Environment.IsDevelopment();

			// Runs no checks at all: this answers "is the process up", which is what a container
			// HEALTHCHECK and a restart policy want. Readiness is the one with opinions.
			app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
			   .AllowAnonymous()
			   .WithName("HealthLive");

			app.MapHealthChecks("/health/ready", new HealthCheckOptions {
				   Predicate = registration => registration.Tags.Contains(ReadyTag),
				   ResponseWriter = (context, report) => WriteReportAsync(context, report, includeExceptionDetail)
			   })
			   .AllowAnonymous()
			   .WithName("HealthReady");
		}
	}

	/// <summary>
	/// Writes the report as a status document rather than RFC 7807. The problem-details convention
	/// elsewhere describes a request that failed; this describes a system that is running, which is not
	/// the same shape even when the status code is 503.
	/// </summary>
	private static async Task WriteReportAsync(HttpContext context, HealthReport report, bool includeExceptionDetail)
	{
		context.Response.ContentType = "application/json; charset=utf-8";

		var payload = new {
			status = report.Status.ToString(),
			totalDurationMs = (long)report.TotalDuration.TotalMilliseconds,
			checks = report.Entries.Select(entry => new {
				name = entry.Key,
				status = entry.Value.Status.ToString(),
				durationMs = (long)entry.Value.Duration.TotalMilliseconds,
				description = entry.Value.Description,
				data = entry.Value.Data.Count == 0 ? null : entry.Value.Data,

				// Exception text can name hosts, paths and credentials, and this endpoint is anonymous
				// and reachable through the proxy. Outside Development the authored description is all a
				// caller gets.
				exception = includeExceptionDetail ? entry.Value.Exception?.Message : null
			})
		};

		await context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions), context.RequestAborted);
	}

	private static readonly JsonSerializerOptions SerializerOptions = new() {
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};
}
