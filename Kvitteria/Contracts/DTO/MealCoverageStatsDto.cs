namespace Kvitteria.Contracts.DTO;

/// <summary>A day-tracked meal over a run of months, oldest first.</summary>
public record MealCoverageStatsDto(Guid MealId, string DisplayName, List<MealCoverageMonthDto> Months);

/// <summary>
/// <paramref name="Spend"/> is every line carrying the meal that month, whether or not its receipt has
/// coverage, and <paramref name="DaysCovered"/> only counts receipts that do, so a household that fills in
/// coverage only sometimes sees a high <paramref name="CostPerDay"/>. Null when no days are covered.
/// </summary>
public record MealCoverageMonthDto(string Month, decimal Spend, int DaysCovered, decimal? CostPerDay);
