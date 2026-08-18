using System.Globalization;
using Backend.Features.Extraction;
using Backend.Features.Mail;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Persistence.Repository.Interface;
using Backend.Features.Shared;
namespace Backend.Features.Persistence.Factory;

public sealed class ReceiptFactoryWorker(
	IServiceScopeFactory scopeFactory,
	PersistenceQueue queue,
	ILogger<ReceiptFactoryWorker> logger
) : BackgroundService
{
	private static readonly TimeZoneInfo DanishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

	/// <summary>
	/// The format <c>ReceiptExtractionPrompt</c> asks for, plus the variants the model drifts to in
	/// practice. Parsing is strict-with-a-fallback rather than <see cref="DateTime.Parse(string)"/>,
	/// because this is unvalidated model output and a throw here used to kill the worker outright.
	/// </summary>
	private static readonly string[] PurchaseDateFormats = [
		"yyyy-MM-dd HH:mm:ss",
		"yyyy-MM-ddTHH:mm:ss",
		"yyyy-MM-dd HH:mm",
		"yyyy-MM-dd"
	];

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		await foreach (ReceiptRecord record in queue.DequeueAllAsync(ct))
		{
			try
			{
				await PersistAsync(record, ct);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				logger.LogInformation("Receipt factory stopped by shutdown while handling '{Subject}'.", record.Subject);
				return;
			}
			catch (Exception ex)
			{
				// One bad receipt must never kill the worker — this is the last pipeline stage, so its
				// death would silently strand everything upstream.
				record.Status = ReceiptStatus.Failed;
				record.Error = ex.Message;
				record.CompletedAt = DateTimeOffset.UtcNow;
				logger.LogError(ex, "Persisting receipt failed for '{Subject}' ({MessageId})", record.Subject, record.MessageId);
			}
		}
	}

	private async Task PersistAsync(ReceiptRecord record, CancellationToken ct)
	{
		if (record.Extracted is null)
		{
			logger.LogWarning("Skipping '{Subject}': no extraction to persist.", record.Subject);
			return;
		}

		using IServiceScope scope = scopeFactory.CreateScope();
		var receiptRepository = scope.ServiceProvider.GetRequiredService<IReceiptRepository>();

		logger.LogDebug("Persisting receipt for '{Subject}' from {Merchant}.", record.Subject, record.Extracted.Merchant);

		Receipt receipt = await BuildReceiptAsync(receiptRepository, record, record.Extracted, ct);

		if (!await receiptRepository.InsertReceipt(receipt, ct))
		{
			// The repository logs the cause and reports failure; without this the receipt would be
			// dropped silently and the record left claiming success.
			throw new InvalidOperationException($"Repository refused to insert the receipt for '{record.Subject}'.");
		}

		record.Status = ReceiptStatus.Completed;
		record.CompletedAt = DateTimeOffset.UtcNow;

		logger.LogInformation(
			"Persisted receipt {ReceiptId} for '{Subject}': {LineCount} lines, total {GrandTotal}, reconciled={Reconciled}",
			receipt.Id, record.Subject, receipt.ReceiptLines.Count, receipt.GrandTotal, receipt.Reconciled);
	}

	private async Task<Receipt> BuildReceiptAsync(
		IReceiptRepository receiptRepository,
		ReceiptRecord record,
		ExtractedReceipt extracted,
		CancellationToken ct)
	{
		var receipt = new Receipt {
			MerchantId = await ResolveMerchantIdAsync(receiptRepository, extracted.Merchant, ct),
			PurchaseDate = ResolvePurchaseDateUtc(extracted.PurchaseDate, record, logger),
			GrandTotal = extracted.GrandTotal,
			PantTotal = extracted.PantTotal,
			Reconciled = record.Reconciled,
			Status = ReceiptStatus.Completed,
			SourceMessageId = record.MessageId,
			Error = record.Error,
			ReceiptLines = extracted.LineItems.Select(l => new ReceiptLine {
				RawName = l.RawName,
				Quantity = l.Quantity,
				UnitPrice = l.UnitPrice,
				LineTotal = l.LineTotal
			}).ToList()
		};

		await LinkCanonicalItemsAsync(receiptRepository, receipt, ct);

		return receipt;
	}

	private static async Task<Guid> ResolveMerchantIdAsync(
		IReceiptRepository receiptRepository, string merchantName, CancellationToken ct)
	{
		Merchant? merchant = await receiptRepository.GetMerchantByName(merchantName, ct)
		                     ?? await receiptRepository.InsertMerchant(merchantName, ct);

		// Previously this fell back to Guid.Empty, which produced a receipt that violated the merchant
		// foreign key — an error the repository then swallowed, losing the receipt without a trace.
		return merchant?.Id
		       ?? throw new InvalidOperationException($"Could not resolve or create merchant '{merchantName}'.");
	}

	private async Task LinkCanonicalItemsAsync(
		IReceiptRepository receiptRepository, Receipt receipt, CancellationToken ct)
	{
		// Normalize once per line rather than once in the projection and again in the loop.
		List<(ReceiptLine Line, string Normalized)> lines = receipt.ReceiptLines
			.Select(l => (Line: l, Normalized: l.RawName.NormalizeString()))
			.ToList();

		string[] normalizedNames = lines.Select(l => l.Normalized).Distinct().ToArray();

		Dictionary<string, Guid> canonicalIdByAlias =
			await receiptRepository.GetCanonicalIdsByRawNames(ct, normalizedNames);

		var unmatched = new List<string>();

		foreach ((ReceiptLine line, string normalized) in lines)
		{
			if (canonicalIdByAlias.TryGetValue(normalized, out Guid canonicalId))
				line.CanonicalItemId = canonicalId;
			else
				unmatched.Add(line.RawName);
		}

		// One summary line, not one warning per unmatched product — on a database with no aliases yet
		// the per-line version emitted a warning for every line of every receipt.
		if (unmatched.Count > 0)
		{
			logger.LogInformation(
				"{UnmatchedCount}/{LineCount} lines have no canonical item yet: {RawNames}",
				unmatched.Count, lines.Count, unmatched);
		}
	}

	/// <summary>
	/// Converts the model's Copenhagen-local purchase date to UTC, falling back to the mail's own
	/// timestamp when the model returns something unparseable.
	/// </summary>
	internal static DateTime ResolvePurchaseDateUtc(string purchaseDate, ReceiptRecord record, ILogger logger)
	{
		if (!TryParseDanishLocal(purchaseDate, out DateTime localDate))
		{
			logger.LogWarning(
				"Unparseable purchase date '{PurchaseDate}' for '{Subject}'; falling back to the mail date {MailDate}.",
				purchaseDate, record.Subject, record.Date);
			return record.Date.UtcDateTime;
		}

		return ToUtc(localDate);
	}

	internal static bool TryParseDanishLocal(string purchaseDate, out DateTime localDate) =>
		DateTime.TryParseExact(
			purchaseDate,
			PurchaseDateFormats,
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out localDate);

	internal static DateTime ToUtc(DateTime localDate)
	{
		// A receipt stamped inside the spring-forward gap (e.g. 02:30 on the last Sunday in March)
		// names a wall-clock time that never existed; ConvertTimeToUtc would throw. Nudge it past the
		// transition rather than failing the whole receipt.
		if (DanishTimeZone.IsInvalidTime(localDate))
			localDate = localDate.AddHours(1);

		return TimeZoneInfo.ConvertTimeToUtc(localDate, DanishTimeZone);
	}
}
