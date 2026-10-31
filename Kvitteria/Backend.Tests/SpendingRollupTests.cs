using Backend.Features.Insights;
using Kvitteria.Contracts.DTO;

namespace Backend.Tests;

public class SpendingRollupTests
{
	private static readonly MonthKey Aug = new(2026, 8);
	private static readonly Guid Dairy = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

	private static DateTime At(int month, int day = 15) => new(2026, month, day, 12, 0, 0, DateTimeKind.Utc);

	private static ReceiptFigure Receipt(int month, decimal grand, decimal pant = 0m) =>
		new(At(month), grand, pant);

	private static LineFigure Line(int month, decimal total, Guid? category = null) => new(At(month), total, category);

	[Fact]
	public void Total_is_the_sum_of_grand_totals_including_pant_and_lines_exclude_it()
	{
		SpendingSummaryDto summary = SpendingRollup.Summary(
			Aug,
			[Receipt(8, 110m, pant: 10m), Receipt(8, 50m)],
			[Line(8, 100m, Dairy), Line(8, 50m, Dairy)]);

		Assert.Equal("2026-08", summary.Month);
		Assert.Equal(160m, summary.Total);
		Assert.Equal(2, summary.ReceiptCount);
		Assert.Equal(10m, summary.PantTotal);
		Assert.Equal(150m, summary.LinesTotal);
	}

	[Fact]
	public void Lines_with_no_category_are_summed_separately_and_include_discounts()
	{
		SpendingSummaryDto summary = SpendingRollup.Summary(
			Aug,
			[Receipt(8, 90m)],
			[Line(8, 100m), Line(8, -10m), Line(8, 25m, Dairy)]);

		Assert.Equal(90m, summary.UncategorizedTotal);
		Assert.Equal(115m, summary.LinesTotal);
	}

	[Fact]
	public void An_empty_month_is_all_zero_and_has_no_change()
	{
		SpendingSummaryDto summary = SpendingRollup.Summary(Aug, [], []);

		Assert.Equal(0m, summary.Total);
		Assert.Equal(0, summary.ReceiptCount);
		Assert.Equal(0m, summary.PantTotal);
		Assert.Equal(0m, summary.LinesTotal);
		Assert.Equal(0m, summary.UncategorizedTotal);
		Assert.Equal(0m, summary.PreviousTotal);
		Assert.Null(summary.ChangePercent);
	}

	[Fact]
	public void Other_months_do_not_leak_into_the_summary()
	{
		SpendingSummaryDto summary = SpendingRollup.Summary(
			Aug,
			[Receipt(7, 999m, pant: 9m), Receipt(8, 100m), Receipt(9, 888m)],
			[Line(7, 999m), Line(8, 100m), Line(9, 888m)]);

		Assert.Equal(100m, summary.Total);
		Assert.Equal(1, summary.ReceiptCount);
		Assert.Equal(0m, summary.PantTotal);
		Assert.Equal(100m, summary.LinesTotal);
		Assert.Equal(999m, summary.PreviousTotal);
	}

	[Fact]
	public void Spending_that_rose_gives_a_positive_change()
	{
		SpendingSummaryDto summary = SpendingRollup.Summary(Aug, [Receipt(7, 1000m), Receipt(8, 1100m)], []);

		Assert.Equal(1000m, summary.PreviousTotal);
		Assert.Equal(10m, summary.ChangePercent);
	}

	[Fact]
	public void No_previous_spending_gives_a_null_change()
	{
		SpendingSummaryDto summary = SpendingRollup.Summary(Aug, [Receipt(8, 100m)], []);

		Assert.Equal(0m, summary.PreviousTotal);
		Assert.Null(summary.ChangePercent);
	}

	[Fact]
	public void A_purchase_just_after_midnight_on_the_first_counts_towards_the_new_month()
	{
		// 00:30 on 1 August in Copenhagen is 22:30 UTC on 31 July.
		var late = new ReceiptFigure(new DateTime(2026, 7, 31, 22, 30, 0, DateTimeKind.Utc), 100m, 0m);

		Assert.Equal(1, SpendingRollup.Summary(Aug, [late], []).ReceiptCount);
		Assert.Equal(0, SpendingRollup.Summary(Aug.Previous(), [late], []).ReceiptCount);
	}

	[Fact]
	public void Trend_lists_every_month_oldest_first_including_empty_ones()
	{
		List<SpendingTrendMonthDto> trend = SpendingRollup.Trend(
			Aug.RunEndingHere(3),
			[Receipt(6, 100m), Receipt(8, 150m), Receipt(8, 50m)]);

		Assert.Equal(["2026-06", "2026-07", "2026-08"], trend.Select(t => t.Month));
		Assert.Equal([100m, 0m, 200m], trend.Select(t => t.Total));
		Assert.Equal([1, 0, 2], trend.Select(t => t.ReceiptCount));
	}

	[Fact]
	public void Trend_changes_follow_the_previous_month_and_are_null_after_an_empty_one()
	{
		List<SpendingTrendMonthDto> trend = SpendingRollup.Trend(
			Aug.RunEndingHere(3),
			[Receipt(6, 100m), Receipt(8, 200m)]);

		Assert.Equal(-100m, trend[1].ChangePercent);
		Assert.Null(trend[2].ChangePercent);
	}

	[Fact]
	public void The_first_trend_month_is_compared_with_the_month_before_the_run()
	{
		List<SpendingTrendMonthDto> trend = SpendingRollup.Trend(
			Aug.RunEndingHere(2),
			[Receipt(6, 100m), Receipt(7, 150m), Receipt(8, 150m)]);

		Assert.Equal(50m, trend[0].ChangePercent);
		Assert.Equal(0m, trend[1].ChangePercent);
	}

	[Fact]
	public void The_first_trend_month_has_no_change_when_nothing_precedes_the_run()
	{
		List<SpendingTrendMonthDto> trend = SpendingRollup.Trend(Aug.RunEndingHere(1), [Receipt(8, 150m)]);

		Assert.Null(trend[0].ChangePercent);
	}

	[Fact]
	public void A_trend_across_a_year_boundary_walks_back_correctly()
	{
		var jan = new MonthKey(2026, 1);
		List<SpendingTrendMonthDto> trend = SpendingRollup.Trend(
			jan.RunEndingHere(2),
			[new ReceiptFigure(new DateTime(2025, 12, 10, 12, 0, 0, DateTimeKind.Utc), 100m, 0m), Receipt(1, 120m)]);

		Assert.Equal(["2025-12", "2026-01"], trend.Select(t => t.Month));
		Assert.Equal(20m, trend[1].ChangePercent);
	}
}
