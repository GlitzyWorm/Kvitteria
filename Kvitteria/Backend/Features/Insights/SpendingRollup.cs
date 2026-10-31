using Kvitteria.Contracts.DTO;
namespace Backend.Features.Insights;

/// <summary>
/// Month totals. The total paid is the sum of grand totals, which include pant, while line figures come
/// from lines and don't, so the two only agree after allowing for pant.
/// </summary>
public static class SpendingRollup
{
	private sealed record ReceiptTotals(decimal Total, int Count, decimal Pant);

	private static readonly ReceiptTotals None = new(0m, 0, 0m);

	public static SpendingSummaryDto Summary(
		MonthKey month,
		IEnumerable<ReceiptFigure> receipts,
		IEnumerable<LineFigure> lines)
	{
		Dictionary<MonthKey, ReceiptTotals> byMonth = ReceiptsByMonth(receipts);
		ReceiptTotals current = byMonth.GetValueOrDefault(month, None);
		ReceiptTotals previous = byMonth.GetValueOrDefault(month.Previous(), None);

		decimal linesTotal = 0m;
		decimal uncategorized = 0m;

		foreach (LineFigure line in lines.Where(l => MonthKey.Of(l.PurchaseDate) == month))
		{
			linesTotal += line.LineTotal;

			if (line.CategoryId == null)
				uncategorized += line.LineTotal;
		}

		return new SpendingSummaryDto(
			month.ToString(),
			current.Total,
			current.Count,
			current.Pant,
			linesTotal,
			uncategorized,
			previous.Total,
			Percent.Change(previous.Total, current.Total));
	}

	/// <summary>
	/// One entry per month of <paramref name="run"/>, oldest first, empty months included. The first entry
	/// is compared with the month before the run, so <paramref name="receipts"/> should reach back to it.
	/// </summary>
	public static List<SpendingTrendMonthDto> Trend(IReadOnlyList<MonthKey> run, IEnumerable<ReceiptFigure> receipts)
	{
		Dictionary<MonthKey, ReceiptTotals> byMonth = ReceiptsByMonth(receipts);
		var result = new List<SpendingTrendMonthDto>(run.Count);

		foreach (MonthKey month in run)
		{
			ReceiptTotals current = byMonth.GetValueOrDefault(month, None);
			ReceiptTotals previous = byMonth.GetValueOrDefault(month.Previous(), None);

			result.Add(new SpendingTrendMonthDto(
				month.ToString(),
				current.Total,
				current.Count,
				Percent.Change(previous.Total, current.Total)));
		}

		return result;
	}

	private static Dictionary<MonthKey, ReceiptTotals> ReceiptsByMonth(IEnumerable<ReceiptFigure> receipts) =>
		receipts.GroupBy(r => MonthKey.Of(r.PurchaseDate))
		        .ToDictionary(
			        g => g.Key,
			        g => new ReceiptTotals(g.Sum(r => r.GrandTotal), g.Count(), g.Sum(r => r.PantTotal)));
}
