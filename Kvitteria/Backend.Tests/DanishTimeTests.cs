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
}
