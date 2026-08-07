using System.Globalization;
using Backend.Features.Extraction.LLM;
using Backend.Features.Mail;
namespace Backend.Features.Extraction;

public class ExtractionWorker(
	ExtractionQueue queue,
	IServiceScopeFactory scopeFactory,
	ILogger<ExtractionWorker> logger
) : BackgroundService
{
	private const int MaxAttempts = 3;
	private const double RetryTemperature = 0.3;

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		await foreach (Receipt mail in queue.DequeueAllAsync(ct))
		{
			mail.Status = ExtractionStatus.Extracting;

			try
			{
				using IServiceScope scope = scopeFactory.CreateScope();
				var client = scope.ServiceProvider.GetRequiredService<OllamaClient>();

				await ExtractWithRetriesAsync(client, mail, ct);

				mail.Status = ExtractionStatus.Extracted;
				
				logger.LogInformation(
					"Extracted '{Subject}': merchant={Merchant}, total={GrandTotal} {Currency}, {LineCount} lines, reconciled={Reconciled} ({Attempts} attempt(s))",
					mail.Subject, mail.Extracted!.Merchant, mail.Extracted.GrandTotal, mail.Extracted.Currency,
					mail.Extracted.LineItems.Count, mail.Reconciled, mail.Attempts);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				mail.Status = ExtractionStatus.Failed;
				mail.Error = "Extraction interrupted by shutdown";
				return;
			}
			catch (Exception ex)
			{
				// One bad receipt must never kill the worker.
				mail.Status = ExtractionStatus.Failed;
				mail.Error = ex.Message;
				mail.CompletedAt = DateTimeOffset.UtcNow;
				logger.LogError(ex, "Extraction failed for '{Subject}' ({Id})", mail.Subject, mail.MessageId);
			}
		}
	}

	private async Task ExtractWithRetriesAsync(OllamaClient client, Receipt mail, CancellationToken ct)
	{
		ExtractedReceipt? best = null;
		var bestDelta = decimal.MaxValue;
		var attempt = 0;
		var reconciled = false;
		string? hint = null;

		while (attempt < MaxAttempts && !reconciled)
		{
			attempt++;
			double temperature = attempt == 1 ? 0 : RetryTemperature;
			ExtractedReceipt result = await client.ExtractAsync(mail.BodyText, hint, temperature, ct);

			decimal delta = 0; // TODO: Fix Grand total extraction
			// decimal delta = result.GrandTotal-result.LineItems.Sum(l => l.LineTotal)+result.PantTotal;

			reconciled = Math.Abs(delta) == 0;

			if (best is null || reconciled || Math.Abs(delta) < Math.Abs(bestDelta))
			{
				best = result;
				bestDelta = delta;
			}

			if (!reconciled)
			{
				logger.LogDebug(
					"Attempt {Attempt}/{MaxAttempts} for '{Subject}' did not reconcile (delta {Delta}). "+
					"\nGrand total: {GrandTotal}"+
					"\nList of items: {Items}"+
					"\nLine totals: {LineTotals}",
					attempt, MaxAttempts, mail.Subject, delta, result.GrandTotal, result.LineItems.Select(l => new { l.RawName, l.Qty, l.LineTotal }), result.LineItems.Sum(l => l.LineTotal));
				hint = string.Create(CultureInfo.InvariantCulture,
					$"Your previous extraction of this receipt was rejected: the line totals plus pant summed to {result.LineItems.Sum(l => l.LineTotal)+result.PantTotal:0.00}, but the receipt's grand total is {result.GrandTotal:0.00}. You probably missed a product, duplicated one, or got an amount wrong. Re-read the receipt and extract it again — especially check the FIRST product right after the store header and the LAST product right before the totals section, and check that no printed amount was multiplied by its quantity.");
			}
		}

		if (!reconciled)
		{
			mail.Error = "Failed to reconcile receipt after {MaxAttempts} attempts. Best delta: {bestDelta:0.00}.";
		}

		mail.Extracted = best;
		mail.Reconciled = reconciled;
		mail.Attempts = attempt;
	}
}