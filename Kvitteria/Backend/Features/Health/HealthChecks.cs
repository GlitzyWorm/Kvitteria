using Backend.Features.Auth;
using Backend.Features.Extraction.LLM;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model;
using Backend.Features.Persistence.Model.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
namespace Backend.Features.Health;

/// <summary>
/// Reports whether the database is answering at all. Everything else in the pipeline depends on this,
/// so it is the first thing worth knowing.
/// </summary>
internal sealed class PostgresHealthCheck(KvitteriaDbContext dbContext) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
	{
		if (await dbContext.Database.CanConnectAsync(ct))
			return HealthCheckResult.Healthy();

		// Host and port only. This endpoint is unauthenticated, and the connection string carries the
		// password.
		var builder = new NpgsqlConnectionStringBuilder(dbContext.Database.GetConnectionString());

		return HealthCheckResult.Unhealthy($"No connection to Postgres at {builder.Host}:{builder.Port}.");
	}
}

/// <summary>
/// Catches the case where the image is newer than the schema. Migrations are applied at startup, so a
/// pending one here means that ran but did not finish.
/// </summary>
internal sealed class MigrationsHealthCheck(KvitteriaDbContext dbContext) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
	{
		List<string> pending = [.. await dbContext.Database.GetPendingMigrationsAsync(ct)];

		if (pending.Count == 0)
			return HealthCheckResult.Healthy();

		return HealthCheckResult.Unhealthy(
			$"{pending.Count} migration(s) have not been applied.",
			data:new Dictionary<string, object> { ["pending"] = pending });
	}
}

/// <summary>Reports whether the extraction model server is reachable.</summary>
internal sealed class OllamaHealthCheck(OllamaClient client) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
	{
		try
		{
			await client.GetAvailableModelsAsync(ct);
			return HealthCheckResult.Healthy();
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			return HealthCheckResult.Unhealthy($"Ollama did not answer: {ex.Message}");
		}
	}
}

/// <summary>
/// Reports whether the model the pipeline is configured to use has actually been pulled. Degraded
/// rather than unhealthy: the API is fully functional and the fix is one <c>PUT /api/settings/pipeline</c>
/// away — but every extraction would fail, and nothing else would say so until receipts stopped
/// arriving.
/// </summary>
internal sealed class OllamaModelHealthCheck(KvitteriaDbContext dbContext, OllamaClient client) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
	{
		PipelineSettings? settings = await dbContext.PipelineSettings.AsNoTracking().FirstOrDefaultAsync(ct);

		if (settings == null)
			return HealthCheckResult.Degraded("Pipeline settings have not been seeded yet.");

		List<string> available;

		try
		{
			available = await client.GetAvailableModelsAsync(ct);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			// The sibling check already reports the transport failure; saying so twice adds nothing.
			return HealthCheckResult.Degraded("Could not reach Ollama to verify the configured model.");
		}

		if (available.Contains(settings.OllamaModel, StringComparer.Ordinal))
			return HealthCheckResult.Healthy();

		return HealthCheckResult.Degraded(
			$"'{settings.OllamaModel}' is not pulled in Ollama.",
			data:new Dictionary<string, object> { ["pulled"] = available });
	}
}

/// <summary>
/// Reports whether anybody owns this household yet. Degraded, never unhealthy: an empty household is
/// exactly how a fresh install starts, and the first person to sign in claims it — see
/// <see cref="UserProvisioning"/>. Marking it a failure would make every new deployment look broken.
/// </summary>
internal sealed class HouseholdHealthCheck(KvitteriaDbContext dbContext) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
	{
		bool hasAdmin = await dbContext.AppUsers
		                              .AsNoTracking()
		                              .AnyAsync(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active, ct);

		return hasAdmin
			? HealthCheckResult.Healthy()
			: HealthCheckResult.Degraded("No active admin yet; the first person to sign in claims the household.");
	}
}
