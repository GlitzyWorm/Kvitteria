using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;
namespace Backend.Features.Insights;

public sealed record NamedLine(string RawName, decimal Quantity, decimal LineTotal);

/// <summary>
/// Groups the raw names an item was printed under. Done in memory with <c>NormalizeString</c> rather than in
/// SQL, because <c>lower(btrim(...))</c> would depend on the database's locale for Æ, Ø and Å and could
/// disagree with the C# normalization that aliases are keyed by.
/// </summary>
public static class RawNames
{
	/// <summary>
	/// One entry per normalized name among the priced lines, labelled with its most frequent spelling (ties
	/// broken ordinally), most purchased first and then by label.
	/// </summary>
	public static List<PricedNameDto> Group(IEnumerable<NamedLine> lines) =>
		lines
			.Where(l => PriceHistory.IsPriced(l.Quantity, l.LineTotal))
			.GroupBy(l => l.RawName.NormalizeString())
			.Select(g => new PricedNameDto(
				g.Key,
				g.GroupBy(l => l.RawName)
				 .OrderByDescending(s => s.Count())
				 .ThenBy(s => s.Key, StringComparer.Ordinal)
				 .First().Key,
				g.Count()))
			.OrderByDescending(n => n.PurchaseCount)
			.ThenBy(n => n.Label, StringComparer.Ordinal)
			.ToList();
}
