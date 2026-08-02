using System.Security.Authentication;
using MailKit.Net.Imap;
using Microsoft.Extensions.Options;
namespace Backend.Features.Mail;

public class MailWorker(
	MailFetcher fetcher,
	IOptionsMonitor<ImapOptions> options,
	ILogger<MailWorker> logger
) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					IReadOnlyList<ReceiptEmail> receipts = await fetcher.FetchUnseenAsync(stoppingToken);
					logger.LogDebug("Fetched {Count} unseen receipts", receipts.Count);
					/*foreach (ReceiptEmail email in receipts)
					{
						
					}*/
				} catch (Exception ex) when (ex is AuthenticationException or ImapProtocolException
				or ImapCommandException or InvalidOperationException)
				{
					logger.LogError(ex, "Mail poller failed to fetch unseen receipts");
				}

				await Task.Delay(TimeSpan.FromSeconds(options.CurrentValue.PollIntervalSeconds), stoppingToken);
			}
		} catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// TODO: Graceful shutdown.
		}
	}
}