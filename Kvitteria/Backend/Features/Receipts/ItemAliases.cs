using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Receipts;

public static class ItemAliases
{
	/// <summary>
	/// Makes <paramref name="normalizedRawName"/> resolve to the item, replacing the alias the name already
	/// had. Nothing is saved; the caller owns <c>SaveChanges</c>, and a race on the unique index surfaces there.
	/// </summary>
	public static async Task UpsertAsync(
		KvitteriaDbContext dbContext,
		string normalizedRawName,
		Guid canonicalItemId,
		CancellationToken ct)
	{
		ItemAlias? existing = await dbContext.ItemAliases
		                                     .FirstOrDefaultAsync(ia => ia.RawName == normalizedRawName, ct);

		if (existing != null)
			existing.CanonicalItemId = canonicalItemId;
		else
			dbContext.ItemAliases.Add(new ItemAlias {
				RawName = normalizedRawName,
				CanonicalItemId = canonicalItemId
			});
	}
}
