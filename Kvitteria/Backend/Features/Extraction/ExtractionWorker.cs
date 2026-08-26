using Backend.Features.Extraction.LLM;
using Backend.Features.Mail;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Factory;
using Backend.Features.Persistence.Model;
using Backend.Features.Shared;
using Kvitteria.Contracts.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Extraction;

public sealed class ExtractionWorker(
	ExtractionQueue queue,
	PersistenceQueue persistenceQueue,
	IServiceScopeFactory scopeFactory,
	ILogger<ExtractionWorker> logger
) : QueueWorker<ReceiptRecord>(queue, logger)
{
	protected override string WorkerName => "Extraction";

	protected override string Describe(ReceiptRecord item) => item.Subject;

	protected override void OnFailed(ReceiptRecord item, string error)
	{
		item.Status = ReceiptStatus.Failed;
		item.Error = error;
		item.CompletedAt = DateTimeOffset.UtcNow;
	}

	protected override async Task ProcessAsync(ReceiptRecord mail, CancellationToken ct)
	{
		mail.Status = ReceiptStatus.Extracting;

		// OllamaClient is a transient typed HttpClient; per-item resolution lets the pooled handler
		// rotate.
		using IServiceScope scope = scopeFactory.CreateScope();
		var client = scope.ServiceProvider.GetRequiredService<OllamaClient>();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();
		PipelineSettings settings = await dbContext.PipelineSettings.AsNoTracking().FirstAsync(ct);

		ExtractionOutcome outcome = await ExtractWithRetriesAsync(client, settings, mail.BodyText, mail.Subject, ct);

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
		OllamaClient client, PipelineSettings settings, string bodyText, string subject, CancellationToken ct)
	{
		int maxAttempts = Math.Max(1, settings.OllamaMaxExtractionAttempts);
		decimal tolerance = Math.Abs(settings.OllamaReconciliationToleranceKr);

		ExtractedReceipt? best = null;
		decimal bestDelta = decimal.MaxValue;
		int attempt = 0;
		bool reconciled = false;
		string? hint = null;
		Exception? lastFailure = null;

		while (attempt < maxAttempts && !reconciled)
		{
			attempt++;
			double temperature = attempt == 1 ? 0 : settings.OllamaRetryTemperature;
			ExtractedReceipt result;

			try
			{
				result = await client.ExtractAsync(bodyText, hint, temperature, ct);
			}
			catch (OllamaResponseException ex)
			{
				lastFailure = ex;
				logger.LogWarning(ex,
					"Attempt {Attempt}/{MaxAttempts} for '{Subject}' did not produce a parseable extraction",
					attempt, maxAttempts, subject);
				hint = ReceiptExtractionPrompt.MalformedJsonHint;
				continue;
			}
			catch (OllamaTransportException ex)
			{
				// The model never saw the prompt, so the request is retried unchanged and without a hint.
				lastFailure = ex;
				logger.LogWarning(ex,
					"Attempt {Attempt}/{MaxAttempts} for '{Subject}' failed to reach Ollama; retrying without a hint",
					attempt, maxAttempts, subject);
				continue;
			}

			decimal lineSum = result.LineItems.Sum(l => l.LineTotal);
			decimal delta = Reconciliation.Delta(result.GrandTotal, lineSum, result.PantTotal);

			reconciled = Reconciliation.IsReconciled(delta, tolerance);

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
