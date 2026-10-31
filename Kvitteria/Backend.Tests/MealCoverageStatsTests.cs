using Backend.Features.Insights;
using Kvitteria.Contracts.DTO;

namespace Backend.Tests;

public class MealCoverageStatsTests
{
	private static readonly Guid Dinner = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
	private static readonly Guid Lunch = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
	private static readonly MonthKey Aug = new(2026, 8);

	private static readonly TrackedMeal[] Meals = [new(Dinner, "Aftensmad"), new(Lunch, "Frokost")];

	private static DateTime At(int month) => new(2026, month, 10, 12, 0, 0, DateTimeKind.Utc);

	private static List<MealCoverageStatsDto> Build(int months, MealLineFigure[] lines, CoverageFigure[] coverage) =>
		MealCoverageStats.Build(Meals, Aug.RunEndingHere(months), lines, coverage);

	[Fact]
	public void Spend_with_coverage_is_divided_by_the_covered_days()
	{
		List<MealCoverageStatsDto> stats = Build(
			1,
			[new MealLineFigure(At(8), Dinner, 500m), new MealLineFigure(At(8), Dinner, 400m)],
			[new CoverageFigure(At(8), Dinner, 12)]);

		MealCoverageMonthDto month = stats[0].Months[0];
		Assert.Equal(900m, month.Spend);
		Assert.Equal(12, month.DaysCovered);
		Assert.Equal(75m, month.CostPerDay);
	}

	[Fact]
	public void Spend_with_no_coverage_is_reported_without_a_cost_per_day()
	{
		List<MealCoverageStatsDto> stats = Build(1, [new MealLineFigure(At(8), Dinner, 300m)], []);

		MealCoverageMonthDto month = stats[0].Months[0];
		Assert.Equal(300m, month.Spend);
		Assert.Equal(0, month.DaysCovered);
		Assert.Null(month.CostPerDay);
	}

	[Fact]
	public void A_month_with_neither_is_zero_and_null()
	{
		List<MealCoverageStatsDto> stats = Build(2, [], []);

		MealCoverageMonthDto month = stats[0].Months[1];
		Assert.Equal(0m, month.Spend);
		Assert.Equal(0, month.DaysCovered);
		Assert.Null(month.CostPerDay);
	}

	[Fact]
	public void A_receipt_without_coverage_adds_spend_but_no_days()
	{
		List<MealCoverageStatsDto> stats = Build(
			1,
			[new MealLineFigure(At(8), Dinner, 600m), new MealLineFigure(At(8), Dinner, 300m)],
			[new CoverageFigure(At(8), Dinner, 6)]);

		MealCoverageMonthDto month = stats[0].Months[0];
		Assert.Equal(900m, month.Spend);
		Assert.Equal(6, month.DaysCovered);
		Assert.Equal(150m, month.CostPerDay);
	}

	[Fact]
	public void Cost_per_day_rounds_to_two_decimals()
	{
		List<MealCoverageStatsDto> stats = Build(1, [new MealLineFigure(At(8), Dinner, 100m)], [new CoverageFigure(At(8), Dinner, 3)]);

		Assert.Equal(33.33m, stats[0].Months[0].CostPerDay);
	}

	[Fact]
	public void Rounding_goes_away_from_zero_at_the_midpoint()
	{
		List<MealCoverageStatsDto> stats = Build(1, [new MealLineFigure(At(8), Dinner, 1m)], [new CoverageFigure(At(8), Dinner, 8)]);

		Assert.Equal(0.13m, stats[0].Months[0].CostPerDay);
	}

	[Fact]
	public void Meals_are_kept_apart_and_in_the_order_given()
	{
		List<MealCoverageStatsDto> stats = Build(
			1,
			[new MealLineFigure(At(8), Dinner, 100m), new MealLineFigure(At(8), Lunch, 40m)],
			[new CoverageFigure(At(8), Lunch, 2)]);

		Assert.Equal([Dinner, Lunch], stats.Select(s => s.MealId));
		Assert.Equal(100m, stats[0].Months[0].Spend);
		Assert.Null(stats[0].Months[0].CostPerDay);
		Assert.Equal(20m, stats[1].Months[0].CostPerDay);
	}

	[Fact]
	public void The_run_is_oldest_first_and_other_months_stay_out()
	{
		List<MealCoverageStatsDto> stats = Build(
			2,
			[new MealLineFigure(At(7), Dinner, 70m), new MealLineFigure(At(8), Dinner, 80m), new MealLineFigure(At(5), Dinner, 999m)],
			[]);

		Assert.Equal(["2026-07", "2026-08"], stats[0].Months.Select(m => m.Month));
		Assert.Equal([70m, 80m], stats[0].Months.Select(m => m.Spend));
	}

	[Fact]
	public void No_tracked_meals_gives_an_empty_list() =>
		Assert.Empty(MealCoverageStats.Build([], Aug.RunEndingHere(3), [], []));
}
