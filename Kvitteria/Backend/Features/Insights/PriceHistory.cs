using Kvitteria.Contracts.DTO;
namespace Backend.Features.Insights;

public sealed record PricedLine(DateTime PurchaseDate, decimal Quantity, decimal LineTotal);

/// <summary>
/// An item's price per unit: the sum of line totals over the sum of quantities, which makes weighed goods
/// and multi-buys come out right where averaging printed unit prices would not. Lines with a zero or
/// negative quantity or total are left out so a discount cannot look like a price drop.
/// </summary>
public static class PriceHistory
{
	/// <summary>The rule for a line that carries a price. The item list applies the same rule in SQL.</summary>
	public static bool IsPriced(decimal quantity, decimal lineTotal) => quantity > 0 && lineTotal > 0;

	public static PriceHistoryDto Build(
		Guid itemId,
		string displayName,
		IReadOnlyList<MonthKey> run,
		IEnumerable<PricedLine> lines,
		string? rawNameKey,
		string? rawNameLabel)
	{
		Dictionary<MonthKey, (decimal Price, int Count)> prices = lines
		                                                          .Where(l => IsPriced(l.Quantity, l.LineTotal))
		                                                          .GroupBy(l => MonthKey.Of(l.PurchaseDate))
		                                                          .ToDictionary(
			                                                          g => g.Key,
			                                                          g => (Math.Round(g.Sum(l => l.LineTotal) / g.Sum(l => l.Quantity), 2, MidpointRounding.AwayFromZero), g.Count()));

		List<PriceMonthDto> months = run
		                             .Select(m => prices.TryGetValue(m, out (decimal Price, int Count) p)
			                             ? new PriceMonthDto(m.ToString(), p.Price, p.Count)
			                             : new PriceMonthDto(m.ToString(), null, 0))
		                             .ToList();

		List<decimal> priced = months.Where(m => m.Price != null).Select(m => m.Price!.Value).ToList();

		return new PriceHistoryDto(
			itemId,
			displayName,
			months,
			priced.Count == 0 ? null : priced[^1],
			priced.Count == 0 ? null : priced.Min(),
			priced.Count < 2 ? null : Percent.Change(priced[0], priced[^1]),
			rawNameKey,
			rawNameLabel);
	}
}
