using System.Globalization;
using Backend.Features.Extraction;
using Backend.Features.Mail;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Persistence.Repository.Interface;
using Backend.Features.Shared;
namespace Backend.Features.Persistence.Factory;

public class ReceiptFactoryWorker(
	IServiceScopeFactory scopeFactory,
	PersistenceQueue queue,
	ILogger<ReceiptFactoryWorker> logger
) : BackgroundService
{
	private static readonly TimeZoneInfo DanishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
	
	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		await foreach (ReceiptRecord record in queue.DequeueAllAsync(ct))
		{
			using IServiceScope scope = scopeFactory.CreateScope();
			var receiptRepository = scope.ServiceProvider.GetRequiredService<IReceiptRepository>();
			
			logger.LogInformation("Processing receipt record: {record}", record);
			
			Receipt? receipt = await CreateReceiptAsync(receiptRepository, record, ct);
			if (receipt != null)
				await receiptRepository.InsertReceipt(receipt, ct);
		}
	}

	private async Task<Receipt?> CreateReceiptAsync(IReceiptRepository receiptRepository, ReceiptRecord receiptRecord, CancellationToken ct)
	{
		ExtractedReceipt? extractedReceipt = receiptRecord.Extracted;

		if (extractedReceipt == null)
			return null;

		var receipt = new Receipt {
			PurchaseDate = ParsePurchaseDateUtc(extractedReceipt.PurchaseDate),
			GrandTotal = extractedReceipt.GrandTotal,
			PantTotal = extractedReceipt.PantTotal,
			Reconciled = receiptRecord.Reconciled,
			Status = ReceiptStatus.Completed,
			ReceiptLines = extractedReceipt.LineItems.Select(l => new ReceiptLine {
				RawName = l.RawName,
				Quantity = l.Quantity,
				UnitPrice = l.UnitPrice,
				LineTotal = l.LineTotal
			}).ToList()
		};
		
		receipt.MerchantId = await ResolveMerchantAsync(receiptRepository, extractedReceipt, ct) ?? Guid.Empty;
		receipt = await ResolveCanonicalItemAsync(receiptRepository, receipt, ct);

		logger.LogDebug("Created receipt: {receipt}", receipt);
		
		return receipt;
	}

	private async Task<Guid?> ResolveMerchantAsync(IReceiptRepository receiptRepository, ExtractedReceipt extractedReceipt, CancellationToken ct)
	{
		Merchant? merchant = await receiptRepository.GetMerchantByName(extractedReceipt.Merchant, ct) ?? await receiptRepository.InsertMerchant(extractedReceipt.Merchant, ct);
		return merchant?.Id;
	}
	
	private async Task<Receipt> ResolveCanonicalItemAsync(IReceiptRepository receiptRepository, Receipt receipt, CancellationToken ct)
	{
		string[] normalizedNames = receipt.ReceiptLines
		                                  .Select(l => Normalize(l.RawName))
		                                  .Distinct()
		                                  .ToArray();

		Dictionary<string, Guid> canonicalIdByAlias = await receiptRepository.GetCanonicalIdsByRawNames(ct, normalizedNames);

		foreach (ReceiptLine line in receipt.ReceiptLines)
		{
			if (canonicalIdByAlias.TryGetValue(Normalize(line.RawName), out Guid canonicalId))
			{
				line.CanonicalItemId = canonicalId;
			}
			else
			{
				logger.LogWarning("No canonical item found for raw name: {RawName}", line.RawName);
			}
		}

		return receipt;
	}

	private static string Normalize(string rawName) => rawName.Trim().ToLower();

	private static DateTime ParsePurchaseDateUtc(string purchaseDate)
	{
		DateTime localDate = DateTime.Parse(purchaseDate, CultureInfo.InvariantCulture, DateTimeStyles.None);
		return TimeZoneInfo.ConvertTimeToUtc(localDate, DanishTimeZone);
	}
}