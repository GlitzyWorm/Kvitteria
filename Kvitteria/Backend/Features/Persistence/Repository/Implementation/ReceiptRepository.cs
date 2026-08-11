using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Persistence.Repository.Interface;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Persistence.Repository.Implementation;

public class ReceiptRepository(
	KvitteriaDbContext dbContext,
	ILogger<ReceiptRepository> logger
) : IReceiptRepository
{

	public async Task<bool> InsertReceipt(Receipt receipt, CancellationToken ct)
	{
		try
		{
			logger.LogInformation("Inserting receipt into the database: {@Receipt}", receipt);
			dbContext.Receipts.Add(receipt);
			await dbContext.SaveChangesAsync(ct);
			return true;
		} catch (Exception e)
		{
			logger.LogError(e, "Error inserting receipt into the database.");
			return false;
		}
	}

	public async Task<Merchant?> GetMerchantByName(string merchantName, CancellationToken cancellationToken)
	{
		try
		{
			Merchant? merchant = await dbContext.Merchants
			                                    .FirstOrDefaultAsync(m => m.Name == merchantName, cancellationToken);
			return merchant;
		} catch (Exception e)
		{
			logger.LogError(e, "Error retrieving merchant by name from the database.");
			return null;
		}
	}
	
	public async Task<Merchant?> InsertMerchant(string merchantName, CancellationToken ct)
	{
		try
		{
			var merchant = new Merchant() { Name = merchantName };
			dbContext.Merchants.Add(merchant);
			await dbContext.SaveChangesAsync(ct);
			return merchant;
		} catch (Exception e)
		{
			logger.LogError(e, "Error inserting merchant into the database.");
			return null;
		}
	}

	public async Task<Dictionary<string, Guid>> GetCanonicalIdsByRawNames(CancellationToken cancellationToken, params string[] rawNames)
	{
		Dictionary<string, Guid> canonicalIds = await dbContext.ItemAliases
		                                                       .AsNoTracking()
		                                                       .Where(a => rawNames.Contains(a.RawName))
		                                                       .ToDictionaryAsync(a => a.RawName, a => a.CanonicalItemId, cancellationToken);

		return canonicalIds;
	}
}