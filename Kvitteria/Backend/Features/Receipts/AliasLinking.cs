using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Receipts;

/// <summary>
/// Links lines to canonical items through learned aliases, for ingestion and for lines a member adds or
/// renames. Only touches lines without an item, so a person's assignment is never replaced.
/// </summary>
public static class AliasLinking
{
	/// <summary>Returns how many lines it linked. Does not save.</summary>
	public static async Task<int> LinkAsync(KvitteriaDbContext dbContext, IReadOnlyCollection<ReceiptLine> lines, CancellationToken ct)
	{
		List<(ReceiptLine Line, string Normalized)> open = lines
		                                                   .Where(l => l.CanonicalItemId == null)
		                                                   .Select(l => (Line: l, Normalized: l.RawName.NormalizeString()))
		                                                   .ToList();

		if (open.Count == 0)
			return 0;

		List<string> normalizedNames = open.Select(l => l.Normalized).Distinct().ToList();

		Dictionary<string, AliasTarget> targetByAlias = await dbContext.ItemAliases
		                                                               .AsNoTracking()
		                                                               .Where(a => normalizedNames.Contains(a.RawName))
		                                                               .Select(a => new { a.RawName, Target = new AliasTarget(a.CanonicalItemId, a.CanonicalItem!.CategoryId, a.CanonicalItem.DefaultMealId) })
		                                                               .ToDictionaryAsync(a => a.RawName, a => a.Target, ct);

		int linked = 0;

		foreach ((ReceiptLine line, string normalized) in open)
		{
			if (!targetByAlias.TryGetValue(normalized, out AliasTarget? target))
				continue;

			line.CanonicalItemId = target.CanonicalItemId;
			AliasLearning.FillGaps(line, target.CategoryId, target.DefaultMealId);
			linked++;
		}

		return linked;
	}

	private sealed record AliasTarget(Guid CanonicalItemId, Guid? CategoryId, Guid? DefaultMealId);
}
