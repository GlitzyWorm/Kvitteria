using Backend.Features.Persistence.Model.Receipts;
namespace Backend.Features.Persistence.Repository.Interface;

public interface IReceiptRepository
{
	Task<bool> InsertReceipt(Receipt receipt, CancellationToken ct);
	Task<Merchant?> GetMerchantByName(string merchantName, CancellationToken cancellationToken);
	Task<Merchant?> InsertMerchant(string merchantName, CancellationToken ct);
	Task<Dictionary<string, Guid>> GetCanonicalIdsByRawNames(CancellationToken cancellationToken, params string[] rawNames);
}