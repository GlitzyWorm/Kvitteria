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
	ReceiptIntakeStore store,
	IServiceScopeFactory scopeFactory,
	ILogger<ExtractionWorker> logger
) : QueueWorker<ReceiptRecord>(queue, logger)
{
	protected override string WorkerName => "Extraction";

	protected override string Describe(ReceiptRecord item) => item.Label;

	protected override async Task OnFailedAsync(ReceiptRecord item, string error)
	{
		item.Status = ReceiptStatus.Failed;
		item.Error = error;
		item.CompletedAt = DateTimeOffset.UtcNow;

		// CancellationToken.None: the write must not be lost to the very shutdown that might follow.
		await store.MarkFailedAsync(item.IntakeId, error, item.Attempts, CancellationToken.None);
	}

	protected override async Task ProcessAsync(ReceiptRecord mail, CancellationToken ct)
	{
		// The claim makes a duplicate enqueue harmless: only one worker turns Queued into Extracting.
		if (!await store.ClaimAsync(mail.IntakeId, ct))
		{
			logger.LogInformation("Skipping '{Subject}': it is no longer queued.", mail.Label);
			return;
		}

		mail.Status = ReceiptStatus.Extracting;

		// OllamaClient is a transient typed HttpClient; per-item resolution lets the pooled handler
		// rotate.
		using IServiceScope scope = scopeFactory.CreateScope();
		var client = scope.ServiceProvider.GetRequiredService<OllamaClient>();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();
		PipelineSettings settings = await dbContext.PipelineSettings.AsNoTracking().FirstAsync(ct);

		if (mail.Source == IntakeSource.Upload)
		{
			// A stored transcription is reused by a retry and by restart recovery, so the image is read once.
			if (mail.BodyText.Length == 0 && !await TranscribeAsync(client, mail, ct))
				return;
		}

		ExtractionOutcome outcome;

		try
		{
			outcome = await ExtractWithRetriesAsync(client, settings, mail.BodyText, ReceiptExtractionPrompt.ExtractionPrompt, mail.Label, ct);
		}
		catch (ExtractionExhaustedException ex)
		{
			mail.Attempts = ex.Attempts;
			throw;
		}

		mail.Extracted = outcome.Receipt;
		mail.Reconciled = outcome.Reconciled;
		mail.Attempts = outcome.Attempts;
		mail.Error = outcome.Warning;
		mail.Status = ReceiptStatus.Extracted;

		if (!await store.MarkExtractedAsync(mail.IntakeId, outcome.Attempts, ct))
		{
			logger.LogWarning("Dropping the extraction of '{Subject}': its record changed while it was extracting.", mail.Label);
			return;
		}

		await persistenceQueue.EnqueueAsync(mail, ct);

		logger.LogInformation(
			"Extracted '{Subject}': merchant={Merchant}, total={GrandTotal} {Currency}, {LineCount} lines, reconciled={Reconciled} ({Attempts} attempt(s))",
			mail.Label, outcome.Receipt.Merchant, outcome.Receipt.GrandTotal, outcome.Receipt.Currency,
			outcome.Receipt.LineItems.Count, outcome.Reconciled, outcome.Attempts);
	}

	/// <summary>
	/// Reads an upload's image into text and stores it on the row. Not wrapped in the extraction retry
	/// budget, which would multiply the most expensive call: a transport failure or timeout propagates
	/// and fails the intake with zero attempts, and a member retries it. False means the row changed under
	/// the worker and the result was dropped.
	/// </summary>
	private async Task<bool> TranscribeAsync(OllamaClient client, ReceiptRecord upload, CancellationToken ct)
	{
		byte[] image = await store.LoadImageAsync(upload.ImageId ?? Guid.Empty, ct)
		               ?? throw new InvalidOperationException("The uploaded image no longer exists.");

		logger.LogInformation("Transcribing {Upload} ({Bytes} bytes).", upload.Label, image.Length);

		string text = await client.TranscribeAsync(image, ct);

		// Nothing is stored, so a retry reads the image again rather than extracting from the sentinel.
		if (ReceiptTranscription.IsNoReceipt(text))
			throw new NoReceiptInImageException();

		if (!await store.SaveTranscriptionAsync(upload.IntakeId, text, ct))
		{
			logger.LogWarning("Dropping the transcription of {Upload}: its record changed while it was transcribing.", upload.Label);
			return false;
		}

		upload.BodyText = text;

		logger.LogInformation("Transcribed {Upload}: {Length} chars.", upload.Label, text.Length);

		if (logger.IsEnabled(LogLevel.Debug))
			logger.LogDebug("Transcription of {Upload}:\n{Text}", upload.Label, text);

		return true;
	}

	/// <summary>The model said the image shows no receipt. Its message is the whole recorded error.</summary>
	private sealed class NoReceiptInImageException() : InvalidOperationException(ReceiptTranscription.NoReceiptError);

	/// <summary>
	/// Result of the retry loop. <paramref name="Warning"/> is set when the extraction succeeded but
	/// never reconciled — the receipt is still usable, so this is not a failure.
	/// </summary>
	private readonly record struct ExtractionOutcome(
		ExtractedReceipt Receipt,
		bool Reconciled,
		int Attempts,
		string? Warning);

	/// <summary>Carries the attempt count to <see cref="OnFailedAsync"/>, which stores it on the failure.</summary>
	private sealed class ExtractionExhaustedException(int attempts, Exception? lastFailure)
		: InvalidOperationException($"Failed to extract any valid data after {attempts} attempt(s).", lastFailure)
	{
		public int Attempts { get; } = attempts;
	}

	private async Task<ExtractionOutcome> ExtractWithRetriesAsync(
		OllamaClient client, PipelineSettings settings, string bodyText, string systemPrompt, string subject, CancellationToken ct)
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
				result = await client.ExtractAsync(bodyText, systemPrompt, hint, temperature, ct);
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
			throw new ExtractionExhaustedException(attempt, lastFailure);
		}

		string? warning = reconciled
			? null
			: $"Failed to reconcile receipt after {attempt} attempt(s). Best delta: {bestDelta:0.00}.";

		return new ExtractionOutcome(best, reconciled, attempt, warning);
	}
}
