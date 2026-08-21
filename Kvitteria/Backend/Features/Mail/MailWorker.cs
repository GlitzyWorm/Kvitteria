using Backend.Features.Extraction;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Mail;

public sealed class MailWorker(
	MailFetcher fetcher,
	ExtractionQueue queue,
	IServiceScopeFactory scopeFactory,
	ILogger<MailWorker> logger
) : BackgroundService
{
	/// <summary>Upper bound on the backoff delay, however many failures pile up.</summary>
	private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);

	/// <summary>Doubling stops here so the delay cannot overflow before <see cref="MaxBackoff"/> clamps it.</summary>
	private const int MaxBackoffExponent = 6;

	private const double JitterFraction = 0.2;

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		PipelineSettings settings = await LoadSettingsAsync(ct);
		using var timer = new PeriodicTimer(PollInterval(settings));
		int consecutiveFailures = 0;

		try
		{
			do
			{
				// Read fresh each tick so a settings edit takes effect on the next cycle.
				settings = await LoadSettingsAsync(ct);

				try
				{
					// Mail is flagged Seen only after this callback returns.
					MailPollResult result = await fetcher.FetchUnseenAsync(queue.EnqueueAsync, settings.ImapMaxMessagesPerPoll, ct);

					if (result.Fetched > 0)
						logger.LogInformation("Fetched {Count} unseen receipt(s)", result.Fetched);

					// The fetcher isolates each mailbox, so a partial failure is not a poll failure. Only
					// a poll where nothing reachable remains earns the backoff; otherwise one broken
					// mailbox would slow down the healthy ones.
					if (result.AllFailed)
					{
						consecutiveFailures++;
						logger.LogError(
							"All {Count} mailbox(es) failed to poll ({ConsecutiveFailures} consecutive failure(s)); see the per-mailbox warnings above.",
							result.Attempted, consecutiveFailures);
					}
					else
					{
						if (result.Failed > 0)
						{
							logger.LogWarning(
								"{Failed} of {Attempted} mailbox(es) failed to poll; the rest succeeded, so the poll interval is unchanged.",
								result.Failed, result.Attempted);
						}

						consecutiveFailures = 0;
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// The filter must stay broad: an escape here ends polling for the process lifetime.
					consecutiveFailures++;
					logger.LogError(ex, "Mail poll failed ({ConsecutiveFailures} consecutive failure(s))", consecutiveFailures);
				}

				timer.Period = NextPeriod(consecutiveFailures, settings);
			} while (await timer.WaitForNextTickAsync(ct));
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			logger.LogInformation("Mail worker stopped by shutdown.");
		}
	}

	/// <summary>MailWorker is a singleton, so a scope is created per read for its scoped <see cref="KvitteriaDbContext"/>.</summary>
	private async Task<PipelineSettings> LoadSettingsAsync(CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();
		return await dbContext.PipelineSettings.AsNoTracking().FirstAsync(ct);
	}

	/// <summary>Clamped because <see cref="PeriodicTimer.Period"/> rejects zero.</summary>
	private static TimeSpan PollInterval(PipelineSettings settings) =>
		TimeSpan.FromSeconds(Math.Max(1, settings.ImapPollIntervalSeconds));

	private static TimeSpan NextPeriod(int consecutiveFailures, PipelineSettings settings)
	{
		TimeSpan baseInterval = PollInterval(settings);

		if (consecutiveFailures == 0)
			return baseInterval;

		// Exponential backoff with jitter, so a dead IMAP server is not hammered at a fixed
		// cadence and several members' folders do not retry in lockstep.
		double factor = Math.Pow(2, Math.Min(consecutiveFailures, MaxBackoffExponent));
		TimeSpan backoff = baseInterval*factor;

		if (backoff > MaxBackoff)
			backoff = MaxBackoff;

		return backoff*(1+Random.Shared.NextDouble()*JitterFraction);
	}
}
