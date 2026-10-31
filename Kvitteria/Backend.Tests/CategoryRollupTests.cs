using Backend.Features.Categories;
using Backend.Features.Insights;
using Kvitteria.Contracts.DTO;

namespace Backend.Tests;

public class CategoryRollupTests
{
	private static readonly MonthKey Aug = new(2026, 8);
	private static readonly MonthKey Jul = new(2026, 7);

	private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");

	private static readonly Guid Food = Id(1);
	private static readonly Guid Dairy = Id(2);
	private static readonly Guid Meat = Id(3);
	private static readonly Guid Home = Id(4);
	private static readonly Guid Cleaning = Id(5);

	private static CategoryTree Tree() => new([
		new CategoryNode(Food, null, "Mad", 0),
		new CategoryNode(Dairy, Food, "Mejeri", 0),
		new CategoryNode(Meat, Food, "Kød", 1),
		new CategoryNode(Home, null, "Hjem", 1),
		new CategoryNode(Cleaning, Home, "Rengøring", 0)
	]);

	private static LineFigure Line(MonthKey month, decimal total, Guid? category) =>
		new(new DateTime(month.Year, month.Month, 15, 12, 0, 0, DateTimeKind.Utc), total, category);

	private static CategoryAmountDto RowFor(CategoryBreakdownDto dto, Guid id) => dto.Rows.Single(r => r.CategoryId == id);

	[Fact]
	public void Child_lines_roll_up_into_their_top_level_category()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(
			Tree(), Aug, null, null,
			[Line(Aug, 30m, Dairy), Line(Aug, 70m, Meat)]);

		CategoryAmountDto row = Assert.Single(dto.Rows);
		Assert.Equal(Food, row.CategoryId);
		Assert.Equal("Mad", row.DisplayName);
		Assert.Equal(100m, row.Amount);
	}

	[Fact]
	public void A_line_directly_on_a_top_level_category_counts_towards_it()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(
			Tree(), Aug, null, null,
			[Line(Aug, 20m, Food), Line(Aug, 30m, Dairy)]);

		Assert.Equal(50m, RowFor(dto, Food).Amount);
	}

	[Fact]
	public void Uncategorized_lines_are_reported_apart_from_the_rows()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(
			Tree(), Aug, null, null,
			[Line(Aug, 40m, null), Line(Aug, -5m, null), Line(Aug, 10m, Dairy)]);

		Assert.Equal(35m, dto.Uncategorized!.Amount);
		Assert.Null(dto.Uncategorized.CategoryId);
		Assert.Equal(10m, Assert.Single(dto.Rows).Amount);
	}

	[Fact]
	public void Everything_categorized_leaves_uncategorized_at_zero()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(Tree(), Aug, null, null, [Line(Aug, 10m, Dairy)]);

		Assert.Equal(0m, dto.Uncategorized!.Amount);
	}

	[Fact]
	public void A_month_with_no_lines_is_empty()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(Tree(), Aug, null, null, []);

		Assert.Empty(dto.Rows);
		Assert.Equal(0m, dto.Uncategorized!.Amount);
	}

	[Fact]
	public void Top_level_rows_and_uncategorized_add_up_to_the_lines()
	{
		LineFigure[] lines = [Line(Aug, 30m, Dairy), Line(Aug, 70m, Meat), Line(Aug, 12.5m, Cleaning), Line(Aug, 7m, null)];

		CategoryBreakdownDto dto = CategoryRollup.Breakdown(Tree(), Aug, null, null, lines);

		Assert.Equal(lines.Sum(l => l.LineTotal), dto.Rows.Sum(r => r.Amount) + dto.Uncategorized!.Amount);
	}

	[Fact]
	public void A_moved_category_takes_its_history_to_the_new_parent()
	{
		// Dairy now sits under Hjem, so even a line bought "back when" it was food counts there.
		var moved = new CategoryTree([
			new CategoryNode(Food, null, "Mad", 0),
			new CategoryNode(Dairy, Home, "Mejeri", 0),
			new CategoryNode(Home, null, "Hjem", 1)
		]);

		CategoryBreakdownDto dto = CategoryRollup.Breakdown(moved, Aug, null, null, [Line(Aug, 30m, Dairy)]);

		Assert.Equal(Home, Assert.Single(dto.Rows).CategoryId);
	}

	[Fact]
	public void Rows_are_largest_first()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(
			Tree(), Aug, null, null,
			[Line(Aug, 10m, Cleaning), Line(Aug, 90m, Dairy)]);

		Assert.Equal([Food, Home], dto.Rows.Select(r => r.CategoryId));
	}

	[Fact]
	public void Other_months_are_ignored()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(
			Tree(), Aug, null, null,
			[Line(Aug, 10m, Dairy), Line(Jul, 500m, Dairy), Line(new MonthKey(2026, 9), 500m, Dairy)]);

		Assert.Equal(10m, Assert.Single(dto.Rows).Amount);
	}

	[Fact]
	public void Without_a_comparison_every_comparison_figure_is_null()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(
			Tree(), Aug, null, null,
			[Line(Aug, 10m, Dairy), Line(Jul, 20m, Dairy), Line(Aug, 5m, null)]);

		Assert.Null(dto.CompareToMonth);
		Assert.Null(dto.Rows[0].CompareAmount);
		Assert.Null(dto.Rows[0].ChangePercent);
		Assert.Null(dto.Uncategorized!.CompareAmount);
		Assert.Null(dto.Uncategorized.ChangePercent);
	}

	[Fact]
	public void A_comparison_gives_both_amounts_and_the_change_including_for_uncategorized()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(
			Tree(), Aug, Jul, null,
			[Line(Aug, 110m, Dairy), Line(Jul, 100m, Meat), Line(Aug, 30m, null), Line(Jul, 20m, null)]);

		CategoryAmountDto food = RowFor(dto, Food);
		Assert.Equal("2026-07", dto.CompareToMonth);
		Assert.Equal(110m, food.Amount);
		Assert.Equal(100m, food.CompareAmount);
		Assert.Equal(10m, food.ChangePercent);
		Assert.Equal(30m, dto.Uncategorized!.Amount);
		Assert.Equal(20m, dto.Uncategorized.CompareAmount);
		Assert.Equal(50m, dto.Uncategorized.ChangePercent);
	}

	[Fact]
	public void A_category_new_this_month_has_a_zero_comparison_and_no_change()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(Tree(), Aug, Jul, null, [Line(Aug, 40m, Cleaning)]);

		CategoryAmountDto row = RowFor(dto, Home);
		Assert.Equal(0m, row.CompareAmount);
		Assert.Null(row.ChangePercent);
	}

	[Fact]
	public void A_category_gone_this_month_is_listed_with_zero()
	{
		CategoryBreakdownDto dto = CategoryRollup.Breakdown(
			Tree(), Aug, Jul, null,
			[Line(Aug, 40m, Dairy), Line(Jul, 60m, Cleaning)]);

		CategoryAmountDto gone = RowFor(dto, Home);
		Assert.Equal(0m, gone.Amount);
		Assert.Equal(60m, gone.CompareAmount);
		Assert.Equal(-100m, gone.ChangePercent);
		Assert.Equal([Food, Home], dto.Rows.Select(r => r.CategoryId));
	}

	[Fact]
	public void Drilling_down_lists_children_and_the_rows_sum_to_the_parent()
	{
		LineFigure[] lines = [Line(Aug, 30m, Dairy), Line(Aug, 70m, Meat), Line(Aug, 5m, Food), Line(Aug, 99m, Cleaning), Line(Aug, 8m, null)];

		CategoryBreakdownDto top = CategoryRollup.Breakdown(Tree(), Aug, null, null, lines);
		CategoryBreakdownDto drill = CategoryRollup.Breakdown(Tree(), Aug, null, Food, lines);

		Assert.Equal(Food, drill.ParentCategoryId);
		Assert.Null(drill.Uncategorized);
		Assert.Equal(RowFor(top, Food).Amount, drill.Rows.Sum(r => r.Amount));
		Assert.Equal([Meat, Dairy, Food], drill.Rows.Select(r => r.CategoryId));
	}

	[Fact]
	public void Lines_directly_on_the_parent_appear_as_the_parent_row()
	{
		CategoryBreakdownDto drill = CategoryRollup.Breakdown(Tree(), Aug, null, Food, [Line(Aug, 5m, Food)]);

		CategoryAmountDto row = Assert.Single(drill.Rows);
		Assert.Equal(Food, row.CategoryId);
		Assert.Equal(5m, row.Amount);
	}

	[Fact]
	public void Drill_down_comparison_works_per_child()
	{
		CategoryBreakdownDto drill = CategoryRollup.Breakdown(
			Tree(), Aug, Jul, Food,
			[Line(Aug, 60m, Dairy), Line(Jul, 50m, Dairy)]);

		Assert.Equal(20m, Assert.Single(drill.Rows).ChangePercent);
	}
}
