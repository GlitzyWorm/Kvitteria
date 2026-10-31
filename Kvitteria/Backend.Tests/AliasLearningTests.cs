using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Receipts;
namespace Backend.Tests;

public class AliasLearningTests
{
	private static readonly Guid Category = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
	private static readonly Guid OtherCategory = Guid.Parse("00000000-0000-0000-0000-0000000000c2");
	private static readonly Guid Meal = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
	private static readonly Guid OtherMeal = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

	private static ReceiptLine Line(string rawName) => new() { RawName = rawName };

	[Theory]
	[InlineData("MÆLK")]
	[InlineData("mælk")]
	[InlineData("  Mælk  ")]
	public void Matches_regardless_of_case_and_surrounding_whitespace(string raw) =>
		Assert.Single(AliasLearning.Matching([Line(raw)], "mælk"));

	[Theory]
	[InlineData("ÆBLER", "æbler")]
	[InlineData("RØDGRØD", "rødgrød")]
	[InlineData("  ÅLE  ", "åle")]
	public void Matches_the_danish_letters(string raw, string normalized) =>
		Assert.Single(AliasLearning.Matching([Line(raw)], normalized));

	[Fact]
	public void Does_not_match_a_different_name() =>
		Assert.Empty(AliasLearning.Matching([Line("Letmælk"), Line("Mælk 1L")], "mælk"));

	[Fact]
	public void Returns_only_the_matching_lines()
	{
		ReceiptLine hit = Line("MÆLK");
		List<ReceiptLine> result = AliasLearning.Matching([hit, Line("Smør")], "mælk");
		Assert.Same(hit, Assert.Single(result));
	}

	[Fact]
	public void FillGaps_sets_empty_fields()
	{
		ReceiptLine line = Line("x");
		AliasLearning.FillGaps(line, Category, Meal);
		Assert.Equal(Category, line.CategoryId);
		Assert.Equal(Meal, line.MealId);
	}

	[Fact]
	public void FillGaps_never_replaces_a_set_value()
	{
		ReceiptLine line = Line("x");
		line.CategoryId = OtherCategory;
		line.MealId = OtherMeal;
		AliasLearning.FillGaps(line, Category, Meal);
		Assert.Equal(OtherCategory, line.CategoryId);
		Assert.Equal(OtherMeal, line.MealId);
	}

	[Fact]
	public void FillGaps_fills_only_the_empty_field()
	{
		ReceiptLine line = Line("x");
		line.CategoryId = OtherCategory;
		AliasLearning.FillGaps(line, Category, Meal);
		Assert.Equal(OtherCategory, line.CategoryId);
		Assert.Equal(Meal, line.MealId);
	}

	[Fact]
	public void FillGaps_with_nulls_leaves_the_line_alone()
	{
		ReceiptLine line = Line("x");
		line.MealId = OtherMeal;
		AliasLearning.FillGaps(line, null, null);
		Assert.Null(line.CategoryId);
		Assert.Equal(OtherMeal, line.MealId);
	}
}
