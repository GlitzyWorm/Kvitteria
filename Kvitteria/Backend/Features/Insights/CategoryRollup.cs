using Backend.Features.Categories;
using Kvitteria.Contracts.DTO;
namespace Backend.Features.Insights;

/// <summary>
/// Groups a month's lines by category through today's taxonomy, so a category moved to another parent
/// takes its history with it. Without a parent the rows are the top-level categories; with one they are
/// its children, and lines sitting directly on the parent become a row for the parent itself, so the
/// rows always sum to the parent's amount.
/// </summary>
public static class CategoryRollup
{
	public static CategoryBreakdownDto Breakdown(
		CategoryTree tree,
		MonthKey month,
		MonthKey? compareTo,
		Guid? parentId,
		IEnumerable<LineFigure> lines)
	{
		var amounts = new Dictionary<Guid, decimal>();
		var compareAmounts = new Dictionary<Guid, decimal>();
		decimal uncategorized = 0m;
		decimal compareUncategorized = 0m;

		foreach (LineFigure line in lines)
		{
			MonthKey lineMonth = MonthKey.Of(line.PurchaseDate);
			bool isCurrent = lineMonth == month;

			if (!isCurrent && lineMonth != compareTo)
				continue;

			if (line.CategoryId == null)
			{
				// Lines with no category belong to no subtree, so a drill-down has nothing to say about them.
				if (parentId != null)
					continue;

				if (isCurrent)
					uncategorized += line.LineTotal;
				else
					compareUncategorized += line.LineTotal;

				continue;
			}

			Guid? row = RowOf(tree, line.CategoryId.Value, parentId);

			if (row == null)
				continue;

			Dictionary<Guid, decimal> target = isCurrent ? amounts : compareAmounts;
			target[row.Value] = target.GetValueOrDefault(row.Value) + line.LineTotal;
		}

		bool comparing = compareTo != null;

		List<CategoryAmountDto> rows = amounts.Keys
		                                      .Union(compareAmounts.Keys)
		                                      .Select(id => {
			                                      CategoryNode node = tree.Node(id)!;
			                                      decimal amount = amounts.GetValueOrDefault(id);
			                                      decimal? compare = comparing ? compareAmounts.GetValueOrDefault(id) : null;

			                                      return (Node: node, Dto: Row(id, node.DisplayName, amount, compare));
		                                      })
		                                      .OrderByDescending(r => r.Dto.Amount)
		                                      .ThenByDescending(r => r.Dto.CompareAmount ?? 0m)
		                                      .ThenBy(r => r.Node.SortOrder)
		                                      .ThenBy(r => r.Node.DisplayName, StringComparer.CurrentCultureIgnoreCase)
		                                      .Select(r => r.Dto)
		                                      .ToList();

		CategoryAmountDto? uncategorizedRow = parentId == null
			? Row(null, null, uncategorized, comparing ? compareUncategorized : null)
			: null;

		return new CategoryBreakdownDto(month.ToString(), compareTo?.ToString(), parentId, rows, uncategorizedRow);
	}

	private static CategoryAmountDto Row(Guid? id, string? name, decimal amount, decimal? compare) =>
		new(id, name, amount, compare, compare == null ? null : Percent.Change(compare.Value, amount));

	/// <summary>
	/// The row a category's lines belong to: its top-level ancestor, or when drilling into
	/// <paramref name="parentId"/>, its child of that parent (the parent itself for lines placed directly on
	/// it). Null for a category outside the subtree.
	/// </summary>
	private static Guid? RowOf(CategoryTree tree, Guid categoryId, Guid? parentId)
	{
		Guid? below = null;
		Guid? current = categoryId;

		// Bounded so cyclic data cannot spin forever.
		for (var i = 0; i <= CategoryTree.MaxDepth && current != null; i++)
		{
			CategoryNode? node = tree.Node(current.Value);

			if (node == null)
				return null;

			if (parentId == null)
			{
				if (node.ParentId == null)
					return node.Id;
			}
			else if (node.Id == parentId)
			{
				return below ?? node.Id;
			}

			below = node.Id;
			current = node.ParentId;
		}

		return null;
	}
}
