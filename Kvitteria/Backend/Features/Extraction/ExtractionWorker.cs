using Backend.Features.Extraction.LLM;
using Backend.Features.Mail;
using Backend.Features.Persistence.Factory;
using Backend.Features.Shared;
using Microsoft.Extensions.Options;
namespace Backend.Features.Extraction;

public sealed class ExtractionWorker(
	ExtractionQueue queue,
	PersistenceQueue persistenceQueue,
	IServiceScopeFactory scopeFactory,
	IOptionsMonitor<OllamaOptions> options,
	ILogger<ExtractionWorker> logger
) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		await foreach (ReceiptRecord mail in queue.DequeueAllAsync(ct))
		{
			mail.Status = ReceiptStatus.Extracting;

			try
			{
				// OllamaClient is a transient typed HttpClient. Resolving it per item — rather than
				// injecting it into this singleton worker — keeps the pooled handler rotating.
				using IServiceScope scope = scopeFactory.CreateScope();
				var client = scope.ServiceProvider.GetRequiredService<OllamaClient>();

				ExtractionOutcome outcome = await ExtractWithRetriesAsync(client, mail.BodyText, mail.Subject, ct);

				mail.Extracted = outcome.Receipt;
				mail.Reconciled = outcome.Reconciled;
				mail.Attempts = outcome.Attempts;
				mail.Error = outcome.Warning;
				mail.Status = ReceiptStatus.Extracted;

				await persistenceQueue.EnqueueAsync(mail, ct);

				logger.LogInformation(
					"Extracted '{Subject}': merchant={Merchant}, total={GrandTotal} {Currency}, {LineCount} lines, reconciled={Reconciled} ({Attempts} attempt(s))",
					mail.Subject, outcome.Receipt.Merchant, outcome.Receipt.GrandTotal, outcome.Receipt.Currency,
					outcome.Receipt.LineItems.Count, outcome.Reconciled, outcome.Attempts);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				mail.Status = ReceiptStatus.Failed;
				mail.Error = "Extraction interrupted by shutdown";
				logger.LogInformation("Extraction worker stopped by shutdown while handling '{Subject}'.", mail.Subject);
				return;
			}
			catch (Exception ex)
			{
				// One bad receipt must never kill the worker.
				mail.Status = ReceiptStatus.Failed;
				mail.Error = ex.Message;
				mail.CompletedAt = DateTimeOffset.UtcNow;
				logger.LogError(ex, "Extraction failed for '{Subject}' ({Id})", mail.Subject, mail.MessageId);
			}
		}
	}

	/// <summary>
	/// Result of the retry loop. <paramref name="Warning"/> is set when the extraction succeeded but
	/// never reconciled — the receipt is still usable, so this is not a failure.
	/// </summary>
	private readonly record struct ExtractionOutcome(
		ExtractedReceipt Receipt,
		bool Reconciled,
		int Attempts,
		string? Warning);

	private async Task<ExtractionOutcome> ExtractWithRetriesAsync(
		OllamaClient client, string bodyText, string subject, CancellationToken ct)
	{
		OllamaOptions ollamaOptions = options.CurrentValue;
		int maxAttempts = Math.Max(1, ollamaOptions.MaxExtractionAttempts);
		decimal tolerance = Math.Abs(ollamaOptions.ReconciliationToleranceKr);

		ExtractedReceipt? best = null;
		decimal bestDelta = decimal.MaxValue;
		int attempt = 0;
		bool reconciled = false;
		string? hint = null;
		Exception? lastFailure = null;

		while (attempt < maxAttempts && !reconciled)
		{
			attempt++;
			double temperature = attempt == 1 ? 0 : ollamaOptions.RetryTemperature;
			ExtractedReceipt result;

			try
			{
				result = await client.ExtractAsync(bodyText, hint, temperature, ct);
			}
			catch (OllamaResponseException ex)
			{
				// The model answered with something unusable, so telling it so is truthful.
				lastFailure = ex;
				logger.LogWarning(ex,
					"Attempt {Attempt}/{MaxAttempts} for '{Subject}' did not produce a parseable extraction",
					attempt, maxAttempts, subject);
				hint = ReceiptExtractionPrompt.MalformedJsonHint;
				continue;
			}
			catch (OllamaTransportException ex)
			{
				// The model never saw the prompt. Retry the same request unchanged — carrying a
				// "your JSON was malformed" hint forward here would poison the next attempt.
				lastFailure = ex;
				logger.LogWarning(ex,
					"Attempt {Attempt}/{MaxAttempts} for '{Subject}' failed to reach Ollama; retrying without a hint",
					attempt, maxAttempts, subject);
				continue;
			}

			decimal lineSum = result.LineItems.Sum(l => l.LineTotal);
			decimal delta = result.GrandTotal-lineSum-result.PantTotal;

			reconciled = Math.Abs(delta) <= tolerance;

			if (best is null || reconciled || Math.Abs(delta) < Math.Abs(bestDelta))
			{
				best = result;
				bestDelta = delta;
			}

			if (reconciled)
				break;

			if (logger.IsEnabled(LogLevel.Debug))
			{
				logger.LogDebug(
					"Attempt {Attempt}/{MaxAttempts} for '{Subject}' did not reconcile: delta {Delta}, "+
					"grand total {GrandTotal}, line totals {LineTotals}, items {Items}",
					attempt, maxAttempts, subject, delta, result.GrandTotal, lineSum,
					result.LineItems.Select(l => new { l.RawName, l.Quantity, l.LineTotal }));
			}

			hint = ReceiptExtractionPrompt.ReconciliationHint(lineSum+result.PantTotal, result.GrandTotal);
		}

		if (best is null)
		{
			throw new InvalidOperationException(
				$"Failed to extract any valid data after {attempt} attempt(s).", lastFailure);
		}

		string? warning = reconciled
			? null
			: $"Failed to reconcile receipt after {attempt} attempt(s). Best delta: {bestDelta:0.00}.";

		return new ExtractionOutcome(best, reconciled, attempt, warning);
	}
}
