using Backend.Features.Extraction;
using Backend.Features.Mail;
using Backend.Features.Persistence.Model;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Kvitteria.Contracts.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Persistence.Factory;

/// <summary>
/// Turns an extraction into persisted rows: resolves the merchant, links known aliases to canonical
/// items, and inserts the receipt with its lines.
/// Exceptions propagate; the caller owns failure handling.
/// </summary>
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

		// Read the row rather than trusting the record: the mailbox may have been deleted since the mail
		// was fetched, and the nulled MailboxId is what the foreign key will accept. A missing row means
		// the mail was already resolved, which must not produce a second receipt.
		ReceiptIntake intake = await dbContext.ReceiptIntakes.FirstOrDefaultAsync(i => i.Id == record.IntakeId, ct)
		                       ?? throw new InvalidOperationException(
			                       $"The recorded mail for '{record.Subject}' no longer exists; it was already resolved.");

		Guid merchantId = await ResolveMerchantIdAsync(extracted.Merchant, ct);

		var receipt = new Receipt {
			MerchantId = merchantId,
			PurchaseDate = PurchaseDate.Resolve(extracted.PurchaseDate, record.Date, logger, record.Subject),
			GrandTotal = extracted.GrandTotal,
			PantTotal = extracted.PantTotal,
			Reconciled = record.Reconciled,
			Status = ReceiptStatus.Completed,
			SourceMessageId = record.MessageId,
			AppUserId = intake.AppUserId,
			MailboxId = intake.MailboxId,
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

		// Deleted in the same transaction, so the mail text is held exactly as long as it is unresolved.
		dbContext.ReceiptIntakes.Remove(intake);
		await dbContext.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);

		return receipt;
	}

	private async Task<Guid> ResolveMerchantIdAsync(string merchantName, CancellationToken ct)
	{
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

		if (unmatched.Count > 0)
		{
			logger.LogInformation(
				"{UnmatchedCount}/{LineCount} lines have no canonical item yet: {RawNames}",
				unmatched.Count, lines.Count, unmatched);
		}
	}
}
