using Microsoft.Extensions.Options;
namespace Backend.Features.Update;

/// <summary>
/// Asks GitHub for the newest installable release. The result is held in memory, because a restart simply
/// checks again, which keeps this feature out of the schema and so out of its own rollback calculation.
/// </summary>
public sealed class UpdateChecker(
	IServiceScopeFactory scopeFactory,
	IOptionsMonitor<UpdateOptions> options,
	ILogger<UpdateChecker> logger
) : BackgroundService
{
	/// <summary>Long enough for a restart loop not to spend GitHub's anonymous rate limit.</summary>
	private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);

	private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

	private volatile CheckResult? _lastResult;

	public AvailableRelease? Newest => _lastResult?.Newest;

	public DateTime? LastCheckedAt => _lastResult?.CheckedAt;

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		if (!options.CurrentValue.Enabled)
		{
			logger.LogInformation("Update check is disabled.");
			return;
		}

		try
		{
			await Task.Delay(FirstCheckDelay, ct);

			using var timer = new PeriodicTimer(CheckInterval);

			do
			{
				await CheckAsync(ct);
			}
			while (await timer.WaitForNextTickAsync(ct));
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			logger.LogInformation("Update checker stopped by shutdown.");
		}
	}

	private async Task CheckAsync(CancellationToken ct)
	{
		try
		{
			// The typed client is resolved per check: holding one in a singleton would pin its pooled handler.
			await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
			GitHubReleasesClient client = scope.ServiceProvider.GetRequiredService<GitHubReleasesClient>();

			List<GitHubRelease> releases = await client.GetReleasesAsync(ct);
			_lastResult = new CheckResult(ReleaseSelection.Newest(releases), DateTime.UtcNow);

			logger.LogInformation("Update check: newest available release is {Version}", _lastResult.Newest?.Version ?? "none");
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
		{
			// Nothing depends on this: the last result stands and readiness never hears about it.
			logger.LogWarning(ex, "Update check failed; keeping the last known result");
		}
	}

	private sealed record CheckResult(AvailableRelease? Newest, DateTime CheckedAt);
}
