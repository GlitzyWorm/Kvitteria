using Backend.Features.Extraction;
using Microsoft.Extensions.Options;
namespace Backend.Features.Mail;

public sealed class MailWorker(
	MailFetcher fetcher,
	ExtractionQueue queue,
	IOptionsMonitor<ImapOptions> options,
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
		using var timer = new PeriodicTimer(PollInterval());
		int consecutiveFailures = 0;

		try
		{
			do
			{
				try
				{
					// Mail is flagged Seen only after this callback returns.
					int fetched = await fetcher.FetchUnseenAsync(queue.EnqueueAsync, ct);

					if (fetched > 0)
						logger.LogInformation("Fetched {Count} unseen receipt(s)", fetched);

					consecutiveFailures = 0;
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// The filter must stay broad: an escape here ends polling for the process lifetime.
					consecutiveFailures++;
					logger.LogError(ex, "Mail poll failed ({ConsecutiveFailures} consecutive failure(s))", consecutiveFailures);
				}

				timer.Period = NextPeriod(consecutiveFailures);
			} while (await timer.WaitForNextTickAsync(ct));
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			logger.LogInformation("Mail worker stopped by shutdown.");
		}
	}

	/// <summary>
	/// Read fresh each tick so a <c>reloadOnChange</c> edit to the poll interval takes effect
	/// on the next cycle. Clamped because <see cref="PeriodicTimer.Period"/> rejects zero.
	/// </summary>
	private TimeSpan PollInterval()
	{
		int seconds = Math.Max(1, options.CurrentValue.PollIntervalSeconds);
		return TimeSpan.FromSeconds(seconds);
	}

	private TimeSpan NextPeriod(int consecutiveFailures)
	{
		TimeSpan baseInterval = PollInterval();

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
