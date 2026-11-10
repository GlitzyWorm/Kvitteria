using Backend.Features.Insights;
using Kvitteria.Contracts.DTO;

namespace Backend.Tests;

public class RawNamesTests
{
	private static NamedLine Line(string name, decimal quantity = 1m, decimal total = 10m) => new(name, quantity, total);

	[Fact]
	public void Spelling_variants_share_one_name_labelled_by_the_most_common()
	{
		List<PricedNameDto> names = RawNames.Group([Line("ARLA LETMÆLK 1L"), Line("ARLA LETMÆLK 1L"), Line(" arla letmælk 1l")]);

		PricedNameDto name = Assert.Single(names);
		Assert.Equal("arla letmælk 1l", name.Key);
		Assert.Equal("ARLA LETMÆLK 1L", name.Label);
		Assert.Equal(3, name.PurchaseCount);
	}

	[Fact]
	public void Danish_letters_normalize_independently_of_culture() =>
		Assert.Single(RawNames.Group([Line("SMØR"), Line("smør")]));

	[Fact]
	public void A_tie_between_spellings_is_broken_ordinally()
	{
		PricedNameDto name = Assert.Single(RawNames.Group([Line("b mælk"), Line("B MÆLK")]));

		Assert.Equal("B MÆLK", name.Label);
	}

	[Fact]
	public void Discount_zero_quantity_and_zero_total_lines_are_excluded()
	{
		List<PricedNameDto> names = RawNames.Group([Line("RABAT", 1m, -3m), Line("NUL", 0m, 5m), Line("GRATIS", 1m, 0m), Line("MÆLK")]);

		Assert.Equal(["mælk"], names.Select(n => n.Key));
	}

	[Fact]
	public void Names_are_ordered_by_count_then_label()
	{
		List<PricedNameDto> names = RawNames.Group([Line("B"), Line("A"), Line("C"), Line("C")]);

		Assert.Equal(["C", "A", "B"], names.Select(n => n.Label));
	}
}
