using System.Security.Authentication;
using Backend.Features.Extraction;
using MailKit.Net.Imap;
using Microsoft.Extensions.Options;
namespace Backend.Features.Mail;

public class MailWorker(
	MailFetcher fetcher,
	ExtractionQueue queue,
	IOptionsMonitor<ImapOptions> options,
	ILogger<MailWorker> logger
) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		try
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					IReadOnlyList<ReceiptRecord> receipts = await fetcher.FetchUnseenAsync(ct);

					foreach (ReceiptRecord email in receipts)
					{
						await queue.EnqueueAsync(email, ct);
					}

					logger.LogDebug("Fetched {Count} unseen receipts", receipts.Count);
				} catch (Exception ex) when (ex is AuthenticationException or ImapProtocolException
				or ImapCommandException or InvalidOperationException)
				{
					logger.LogError(ex, "Mail poller failed to fetch unseen receipts");
				}

				await Task.Delay(TimeSpan.FromSeconds(options.CurrentValue.PollIntervalSeconds), ct);
			}
		} catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			// TODO: Graceful shutdown.
		}
	}
}