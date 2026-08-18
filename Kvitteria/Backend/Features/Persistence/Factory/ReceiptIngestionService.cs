using Backend.Features.Extraction;
using Backend.Features.Mail;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Persistence.Factory;

/// <summary>
/// Turns an extraction into persisted rows: resolves the merchant, links known aliases to canonical
/// items, and inserts the receipt with its lines.
/// </summary>
/// <remarks>
/// Replaces IReceiptRepository, which existed for this one caller and whose nullable return types
/// invited a swallow-and-return-null style: a database outage became "not found", which became a
/// Guid.Empty merchant, which became a foreign-key violation, which was swallowed in turn. Nothing
/// here catches — the worker's per-item handler owns failure, so a fault is visible exactly once.
///
/// Concrete rather than interface-backed: there is one implementation and one caller, and the
/// endpoints already inject KvitteriaDbContext directly, so a second persistence convention bought
/// nothing.
/// </remarks>
public sealed class ReceiptIngestionService(
	KvitteriaDbContext dbContext,
	ILogger<ReceiptIngestionService> logger)
{
	/// <summary>
	/// Persists the extraction as a receipt and returns it. Merchant creation and the receipt insert
	/// share one transaction, so a failure cannot leave an orphaned merchant behind.
	/// </summary>
	public async Task<Receipt> IngestAsync(ReceiptRecord record, ExtractedReceipt extracted, CancellationToken ct)
	{
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(ct);

		Guid merchantId = await ResolveMerchantIdAsync(extracted.Merchant, ct);

		var receipt = new Receipt {
			MerchantId = merchantId,
			PurchaseDate = PurchaseDate.Resolve(extracted.PurchaseDate, record.Date, logger, record.Subject),
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

		await LinkCanonicalItemsAsync(receipt, ct);

		dbContext.Receipts.Add(receipt);
		await dbContext.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);

		return receipt;
	}

	private async Task<Guid> ResolveMerchantIdAsync(string merchantName, CancellationToken ct)
	{
		// Merchant.Name is citext + unique, so this match is case-insensitive and the database is what
		// actually guarantees a single row per shop.
		Guid existingId = await dbContext.Merchants
		                                 .Where(m => m.Name == merchantName)
		                                 .Select(m => m.Id)
		                                 .FirstOrDefaultAsync(ct);

		if (existingId != Guid.Empty)
			return existingId;

		var merchant = new Merchant { Name = merchantName };
		dbContext.Merchants.Add(merchant);
		await dbContext.SaveChangesAsync(ct);

		logger.LogInformation("Registered new merchant '{Merchant}'.", merchantName);

		return merchant.Id;
	}

	private async Task LinkCanonicalItemsAsync(Receipt receipt, CancellationToken ct)
	{
		// Normalize once per line, rather than in the projection and again in the loop.
		List<(ReceiptLine Line, string Normalized)> lines = receipt.ReceiptLines
			.Select(l => (Line: l, Normalized: l.RawName.NormalizeString()))
			.ToList();

		List<string> normalizedNames = lines.Select(l => l.Normalized).Distinct().ToList();

		Dictionary<string, Guid> canonicalIdByAlias = await dbContext.ItemAliases
			.AsNoTracking()
			.Where(a => normalizedNames.Contains(a.RawName))
			.ToDictionaryAsync(a => a.RawName, a => a.CanonicalItemId, ct);

		var unmatched = new List<string>();

		foreach ((ReceiptLine line, string normalized) in lines)
		{
			if (canonicalIdByAlias.TryGetValue(normalized, out Guid canonicalId))
				line.CanonicalItemId = canonicalId;
			else
				unmatched.Add(line.RawName);
		}

		// One summary line. Warning-per-line meant a fresh database logged a warning for every line
		// of every receipt.
		if (unmatched.Count > 0)
		{
			logger.LogInformation(
				"{UnmatchedCount}/{LineCount} lines have no canonical item yet: {RawNames}",
				unmatched.Count, lines.Count, unmatched);
		}
	}
}
