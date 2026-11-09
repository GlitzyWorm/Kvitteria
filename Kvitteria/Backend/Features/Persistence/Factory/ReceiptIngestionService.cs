using Backend.Features.Extraction;
using Backend.Features.Mail;
using Backend.Features.Persistence.Model;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Receipts;
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
			                       $"The recorded intake for '{record.Label}' no longer exists; it was already resolved.");

		Guid merchantId = await ResolveMerchantIdAsync(extracted.Merchant, ct);

		// record.Date is the mail's date, or an upload's capture time and otherwise its upload time.
		DateTime purchaseDate = PurchaseDate.Resolve(extracted.PurchaseDate, record.Date, logger, record.Label);

		Guid? possibleDuplicateOfId = await FindPossibleDuplicateAsync(merchantId, extracted.GrandTotal, purchaseDate, ct);

		if (possibleDuplicateOfId is not null)
		{
			logger.LogInformation(
				"'{Subject}' matches receipt {ReceiptId} on merchant, day and total; flagged as a possible duplicate.",
				record.Label, possibleDuplicateOfId);
		}

		var receipt = new Receipt {
			MerchantId = merchantId,
			PurchaseDate = purchaseDate,
			GrandTotal = extracted.GrandTotal,
			PantTotal = extracted.PantTotal,
			Status = ReceiptStatus.Completed,
			SourceMessageId = record.MessageId,
			AppUserId = intake.AppUserId,
			MailboxId = intake.MailboxId,
			// The image moves from the intake to the receipt in this transaction rather than being deleted.
			ImageId = intake.ImageId,
			PossibleDuplicateOfId = possibleDuplicateOfId,
			CreatedAt = DateTime.UtcNow,
			Error = record.Error,
			ReceiptLines = extracted.LineItems.Select(l => new ReceiptLine {
				RawName = l.RawName,
				Quantity = l.Quantity,
				UnitPrice = l.UnitPrice,
				LineTotal = l.LineTotal
			}).ToList()
		};

		int linked = await AliasLinking.LinkAsync(dbContext, receipt.ReceiptLines, ct);

		if (linked < receipt.ReceiptLines.Count)
		{
			logger.LogInformation(
				"{UnmatchedCount}/{LineCount} lines have no canonical item yet.",
				receipt.ReceiptLines.Count - linked, receipt.ReceiptLines.Count);
		}

		dbContext.Receipts.Add(receipt);

		// Deleted in the same transaction, so the mail text or transcription is held exactly as long as it
		// is unresolved.
		dbContext.ReceiptIntakes.Remove(intake);
		await dbContext.SaveChangesAsync(ct);
		await transaction.CommitAsync(ct);

		return receipt;
	}

	/// <summary>
	/// The earliest stored receipt with the same merchant, the same Danish calendar day and the same total,
	/// whatever its source. Exact on purpose: a false flag is noise, a missed one is how things were.
	/// </summary>
	private async Task<Guid?> FindPossibleDuplicateAsync(Guid merchantId, decimal grandTotal, DateTime purchaseDate, CancellationToken ct)
	{
		(DateTime dayStart, DateTime dayEnd) = DanishTime.DayRangeUtc(purchaseDate);

		return await dbContext.Receipts
		                      .AsNoTracking()
		                      .Where(r => r.MerchantId == merchantId &&
		                                  r.GrandTotal == grandTotal &&
		                                  r.PurchaseDate >= dayStart &&
		                                  r.PurchaseDate < dayEnd)
		                      .OrderBy(r => r.CreatedAt)
		                      .ThenBy(r => r.Id)
		                      .Select(r => (Guid?)r.Id)
		                      .FirstOrDefaultAsync(ct);
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
}
