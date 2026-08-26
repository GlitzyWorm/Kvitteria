using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Kvitteria.Contracts.DTO;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Receipts;

/// <summary><summary>The meal-day-coverage invariant.</summary></summary>
public static class MealCoverage
{
	public const int MinDaysCovered = 0;
	public const int MaxDaysCovered = 31;

	/// <summary>Coverage rows to update in place, to delete, and to insert.</summary>
	public sealed record Plan(
		IReadOnlyList<ReceiptMealCoverage> Unchanged,
		IReadOnlyList<ReceiptMealCoverage> Removed,
		IReadOnlyList<ReceiptMealCoverage> Added);

	/// <summary>Duplicate meal ids and out-of-range day counts, neither of which needs the database.</summary>
	public static bool TryValidate(IReadOnlyList<MealCoverageEntryDto> coverage, out IResult problem)
	{
		List<Guid> duplicateIds = coverage.GroupBy(c => c.MealId)
		                                  .Where(g => g.Count() > 1)
		                                  .Select(g => g.Key)
		                                  .ToList();

		if (duplicateIds.Count != 0)
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: $"Coverage contains duplicate meal ids: {string.Join(", ", duplicateIds)}");
			return false;
		}

		if (coverage.Any(c => c.DaysCovered is < MinDaysCovered or > MaxDaysCovered))
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: $"DaysCovered must be between {MinDaysCovered} and {MaxDaysCovered}.");
			return false;
		}

		problem = Results.Empty;
		return true;
	}

	/// <summary>
	/// Reconciles existing coverage against what was requested. Rows present in both are mutated in
	/// place and reported as <see cref="Plan.Unchanged"/>; the caller persists the plan.
	/// </summary>
	public static Plan Diff(
		Guid receiptId,
		IReadOnlyList<ReceiptMealCoverage> existing,
		IReadOnlyDictionary<Guid, int> desired)
	{
		var unchanged = new List<ReceiptMealCoverage>();
		var removed = new List<ReceiptMealCoverage>();

		foreach (ReceiptMealCoverage row in existing)
		{
			if (desired.TryGetValue(row.MealId, out int days))
			{
				row.DaysCovered = days;
				unchanged.Add(row);
			}
			else
			{
				removed.Add(row);
			}
		}

		HashSet<Guid> existingMealIds = existing.Select(c => c.MealId).ToHashSet();

		List<ReceiptMealCoverage> added = desired
			.Where(kvp => !existingMealIds.Contains(kvp.Key))
			.Select(kvp => new ReceiptMealCoverage {
				ReceiptId = receiptId,
				MealId = kvp.Key,
				DaysCovered = kvp.Value
			})
			.ToList();

		return new Plan(unchanged, removed, added);
	}

	public static void Apply(KvitteriaDbContext dbContext, Plan plan)
	{
		dbContext.ReceiptMealCoverages.RemoveRange(plan.Removed);
		dbContext.ReceiptMealCoverages.AddRange(plan.Added);
	}

	/// <summary>
	/// Drops the receipt's coverage row for a meal once no line on that receipt carries it. Coverage is
	/// only meaningful while some line still references the meal.
	/// </summary>
	public static async Task ClearOrphanedAsync(
		KvitteriaDbContext dbContext,
		Guid receiptId,
		Guid mealId,
		Guid excludedReceiptLineId,
		CancellationToken ct)
	{
		// The current line's new MealId is not saved yet, so it is excluded by id rather than by value.
		bool stillAssigned = await dbContext.ReceiptLines
		                                    .AnyAsync(rl => rl.ReceiptId == receiptId
		                                                    && rl.MealId == mealId
		                                                    && rl.Id != excludedReceiptLineId, ct);

		if (stillAssigned)
			return;

		await dbContext.ReceiptMealCoverages
		               .Where(c => c.ReceiptId == receiptId && c.MealId == mealId)
		               .ExecuteDeleteAsync(ct);
	}
}
