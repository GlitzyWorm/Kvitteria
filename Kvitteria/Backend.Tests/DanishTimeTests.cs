using Backend.Features.Shared;
namespace Backend.Tests;

public class DanishTimeTests
{
	[Fact]
	public void Winter_month_starts_one_hour_before_midnight_utc()
	{
		// January is CET, UTC+1.
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

		Assert.Equal(new DateTime(2025, 12, 31, 23, 0, 0, DateTimeKind.Utc), start);
		Assert.Equal(new DateTime(2026, 1, 31, 23, 0, 0, DateTimeKind.Utc), end);
	}

	[Fact]
	public void Summer_month_starts_two_hours_before_midnight_utc()
	{
		// July is CEST, UTC+2.
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));

		Assert.Equal(new DateTime(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc), start);
		Assert.Equal(new DateTime(2026, 7, 31, 22, 0, 0, DateTimeKind.Utc), end);
	}

	[Fact]
	public void An_instant_still_in_the_previous_month_in_utc_belongs_to_the_new_danish_month()
	{
		// 22:30 UTC on 31 July is 00:30 on 1 August in Copenhagen.
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(new DateTimeOffset(2026, 7, 31, 22, 30, 0, TimeSpan.Zero));

		Assert.Equal(new DateTime(2026, 7, 31, 22, 0, 0, DateTimeKind.Utc), start);
		Assert.Equal(new DateTime(2026, 8, 31, 22, 0, 0, DateTimeKind.Utc), end);
	}

	[Fact]
	public void Month_spanning_a_dst_change_uses_each_boundarys_own_offset()
	{
		// March starts in CET (UTC+1) and ends in CEST (UTC+2).
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

		Assert.Equal(new DateTime(2026, 2, 28, 23, 0, 0, DateTimeKind.Utc), start);
		Assert.Equal(new DateTime(2026, 3, 31, 22, 0, 0, DateTimeKind.Utc), end);
	}

	[Fact]
	public void Month_range_by_year_and_month_matches_the_instant_overload()
	{
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(2026, 7);

		Assert.Equal(new DateTime(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc), start);
		Assert.Equal(new DateTime(2026, 7, 31, 22, 0, 0, DateTimeKind.Utc), end);
	}

	[Fact]
	public void Month_range_across_the_december_boundary_rolls_the_year()
	{
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(2025, 12);

		Assert.Equal(new DateTime(2025, 11, 30, 23, 0, 0, DateTimeKind.Utc), start);
		Assert.Equal(new DateTime(2025, 12, 31, 23, 0, 0, DateTimeKind.Utc), end);
	}

	[Theory]
	[InlineData(2026, 3)] // spring forward
	[InlineData(2026, 10)] // fall back
	public void Months_around_a_dst_change_are_contiguous(int year, int month)
	{
		(DateTime prevStart, DateTime prevEnd) = DanishTime.MonthRangeUtc(year, month - 1);
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(year, month);
		DateTime nextStart = DanishTime.MonthRangeUtc(year, month + 1).StartUtc;

		Assert.Equal(prevEnd, start);
		Assert.Equal(end, nextStart);
		Assert.True(prevStart < start);
	}

	[Fact]
	public void March_2026_is_one_hour_shorter_than_31_days()
	{
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(2026, 3);

		Assert.Equal(TimeSpan.FromHours(31 * 24 - 1), end - start);
	}

	[Fact]
	public void October_2026_is_one_hour_longer_than_31_days()
	{
		(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(2026, 10);

		Assert.Equal(TimeSpan.FromHours(31 * 24 + 1), end - start);
	}

	[Fact]
	public void A_purchase_at_00_30_on_the_first_in_summer_belongs_to_the_new_month()
	{
		// 22:30 UTC on 31 July is 00:30 on 1 August in Copenhagen.
		Assert.Equal((2026, 8), DanishTime.MonthOf(new DateTime(2026, 7, 31, 22, 30, 0, DateTimeKind.Utc)));
	}

	[Fact]
	public void The_last_instant_of_a_month_belongs_to_that_month_and_the_next_instant_to_the_next()
	{
		// August ends at 22:00 UTC on 31 August.
		Assert.Equal((2026, 8), DanishTime.MonthOf(new DateTime(2026, 8, 31, 21, 59, 59, 999, DateTimeKind.Utc)));
		Assert.Equal((2026, 9), DanishTime.MonthOf(new DateTime(2026, 8, 31, 22, 0, 0, DateTimeKind.Utc)));
	}

	[Fact]
	public void A_purchase_at_00_30_on_the_first_in_winter_belongs_to_the_new_month()
	{
		// 23:30 UTC on 31 December is 00:30 on 1 January in Copenhagen.
		Assert.Equal((2026, 1), DanishTime.MonthOf(new DateTime(2025, 12, 31, 23, 30, 0, DateTimeKind.Utc)));
	}

	[Fact]
	public void Month_of_ignores_the_kind_it_was_given()
	{
		Assert.Equal((2026, 8), DanishTime.MonthOf(new DateTime(2026, 7, 31, 22, 30, 0, DateTimeKind.Unspecified)));
	}

	[Fact]
	public void Every_instant_around_both_dst_changes_agrees_with_the_range_of_its_month()
	{
		foreach (DateTime instant in new[] {
			         new DateTime(2026, 3, 31, 21, 59, 59, DateTimeKind.Utc),
			         new DateTime(2026, 3, 31, 22, 0, 0, DateTimeKind.Utc),
			         new DateTime(2026, 10, 31, 22, 59, 59, DateTimeKind.Utc),
			         new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc)
		         })
		{
			(int year, int month) = DanishTime.MonthOf(instant);
			(DateTime start, DateTime end) = DanishTime.MonthRangeUtc(year, month);

			Assert.True(instant >= start && instant < end, $"{instant:O} is not inside {year}-{month:00}");
		}
	}
}
