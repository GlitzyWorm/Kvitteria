using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Receipts;
namespace Backend.Tests;

public class MealCoverageTests
{
	private static readonly Guid ReceiptId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
	private static readonly Guid Aftensmad = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
	private static readonly Guid Frokost = Guid.Parse("00000000-0000-0000-0000-0000000000f1");

	[Fact]
	public void Accepts_distinct_meals_within_range() =>
		Assert.True(MealCoverage.TryValidate(
			[new MealCoverageEntryDto(Aftensmad, 3), new MealCoverageEntryDto(Frokost, 0)], out _));

	[Fact]
	public void Accepts_an_empty_request_because_that_clears_coverage() =>
		Assert.True(MealCoverage.TryValidate([], out _));

	[Fact]
	public void Rejects_duplicate_meal_ids() =>
		Assert.False(MealCoverage.TryValidate(
			[new MealCoverageEntryDto(Aftensmad, 1), new MealCoverageEntryDto(Aftensmad, 2)], out _));

	[Theory]
	[InlineData(-1)]
	[InlineData(32)]
	public void Rejects_day_counts_outside_the_allowed_range(int days) =>
		Assert.False(MealCoverage.TryValidate([new MealCoverageEntryDto(Aftensmad, days)], out _));

	[Theory]
	[InlineData(0)]
	[InlineData(31)]
	public void Accepts_the_range_boundaries(int days) =>
		Assert.True(MealCoverage.TryValidate([new MealCoverageEntryDto(Aftensmad, days)], out _));

	[Fact]
	public void Diff_inserts_meals_that_have_no_existing_row()
	{
		MealCoverage.Plan plan = MealCoverage.Diff(ReceiptId, [], new Dictionary<Guid, int> { [Aftensmad] = 3 });

		ReceiptMealCoverage added = Assert.Single(plan.Added);
		Assert.Equal(ReceiptId, added.ReceiptId);
		Assert.Equal(Aftensmad, added.MealId);
		Assert.Equal(3, added.DaysCovered);
		Assert.Empty(plan.Removed);
		Assert.Empty(plan.Unchanged);
	}

	[Fact]
	public void Diff_updates_an_existing_row_in_place()
	{
		var existing = new ReceiptMealCoverage { ReceiptId = ReceiptId, MealId = Aftensmad, DaysCovered = 3 };

		MealCoverage.Plan plan = MealCoverage.Diff(ReceiptId, [existing], new Dictionary<Guid, int> { [Aftensmad] = 7 });

		Assert.Same(existing, Assert.Single(plan.Unchanged));
		Assert.Equal(7, existing.DaysCovered);
		Assert.Empty(plan.Added);
		Assert.Empty(plan.Removed);
	}

	[Fact]
	public void Diff_removes_rows_that_were_not_requested()
	{
		var existing = new ReceiptMealCoverage { ReceiptId = ReceiptId, MealId = Aftensmad, DaysCovered = 3 };

		MealCoverage.Plan plan = MealCoverage.Diff(ReceiptId, [existing], new Dictionary<Guid, int>());

		Assert.Same(existing, Assert.Single(plan.Removed));
		Assert.Empty(plan.Added);
		Assert.Empty(plan.Unchanged);
	}

	[Fact]
	public void Diff_handles_a_mixed_add_update_and_remove()
	{
		var keep = new ReceiptMealCoverage { ReceiptId = ReceiptId, MealId = Aftensmad, DaysCovered = 3 };
		var drop = new ReceiptMealCoverage { ReceiptId = ReceiptId, MealId = Frokost, DaysCovered = 2 };
		Guid snacks = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

		MealCoverage.Plan plan = MealCoverage.Diff(ReceiptId, [keep, drop],
			new Dictionary<Guid, int> { [Aftensmad] = 5, [snacks] = 1 });

		Assert.Same(keep, Assert.Single(plan.Unchanged));
		Assert.Equal(5, keep.DaysCovered);
		Assert.Same(drop, Assert.Single(plan.Removed));
		Assert.Equal(snacks, Assert.Single(plan.Added).MealId);
	}
}
