using Kvitteria.Contracts.DTO;
namespace Backend.Features.Insights;

public sealed record TrackedMeal(Guid Id, string DisplayName);

public sealed record MealLineFigure(DateTime PurchaseDate, Guid MealId, decimal LineTotal);

public sealed record CoverageFigure(DateTime PurchaseDate, Guid MealId, int DaysCovered);

/// <summary>
/// What a day-tracked meal costs per covered day. The numerator is all spend on the meal, so it matches
/// the lines that carry it; receipts with no coverage add to it without adding days.
/// </summary>
public static class MealCoverageStats
{
	/// <param name="meals">The meals that track days, already in display order.</param>
	public static List<MealCoverageStatsDto> Build(
		IReadOnlyList<TrackedMeal> meals,
		IReadOnlyList<MonthKey> run,
		IEnumerable<MealLineFigure> lines,
		IEnumerable<CoverageFigure> coverage)
	{
		Dictionary<(Guid, MonthKey), decimal> spend = lines
		                                              .GroupBy(l => (l.MealId, MonthKey.Of(l.PurchaseDate)))
		                                              .ToDictionary(g => g.Key, g => g.Sum(l => l.LineTotal));

		Dictionary<(Guid, MonthKey), int> days = coverage
		                                         .GroupBy(c => (c.MealId, MonthKey.Of(c.PurchaseDate)))
		                                         .ToDictionary(g => g.Key, g => g.Sum(c => c.DaysCovered));

		return meals.Select(meal => new MealCoverageStatsDto(
			             meal.Id,
			             meal.DisplayName,
			             run.Select(month => {
				             decimal monthSpend = spend.GetValueOrDefault((meal.Id, month));
				             int monthDays = days.GetValueOrDefault((meal.Id, month));

				             return new MealCoverageMonthDto(
					             month.ToString(),
					             monthSpend,
					             monthDays,
					             monthDays == 0 ? null : Math.Round(monthSpend / monthDays, 2, MidpointRounding.AwayFromZero));
			             }).ToList()))
		            .ToList();
	}
}
