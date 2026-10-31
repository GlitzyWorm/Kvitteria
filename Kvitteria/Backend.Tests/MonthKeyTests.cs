using Backend.Features.Insights;

namespace Backend.Tests;

public class MonthKeyTests
{
	[Theory]
	[InlineData("2026-01", 2026, 1)]
	[InlineData("2026-12", 2026, 12)]
	[InlineData("1999-07", 1999, 7)]
	public void Parses_valid_months(string value, int year, int month)
	{
		Assert.True(MonthKey.TryParse(value, out MonthKey key));
		Assert.Equal(new MonthKey(year, month), key);
	}

	[Theory]
	[InlineData("2026-13")]
	[InlineData("2026-00")]
	[InlineData("2026-1")]
	[InlineData("2026-01-01")]
	[InlineData(" 2026-01")]
	[InlineData("26-01")]
	[InlineData("2026/01")]
	[InlineData("-026-01")]
	[InlineData("abcd-ef")]
	[InlineData("garbage")]
	[InlineData("")]
	[InlineData(null)]
	public void Rejects_everything_else(string? value) => Assert.False(MonthKey.TryParse(value, out _));

	[Fact]
	public void Formats_with_leading_zeros() => Assert.Equal("2026-03", new MonthKey(2026, 3).ToString());

	[Fact]
	public void Walks_back_across_a_year_boundary()
	{
		Assert.Equal(new MonthKey(2025, 12), new MonthKey(2026, 1).Previous());
		Assert.Equal(new MonthKey(2025, 11), new MonthKey(2026, 1).AddMonths(-2));
		Assert.Equal(new MonthKey(2026, 2), new MonthKey(2025, 11).AddMonths(3));
	}

	[Fact]
	public void A_run_ends_at_the_given_month_and_is_oldest_first()
	{
		List<MonthKey> run = new MonthKey(2026, 2).RunEndingHere(4);

		Assert.Equal(["2025-11", "2025-12", "2026-01", "2026-02"], run.Select(m => m.ToString()));
	}

	[Fact]
	public void A_run_of_one_is_just_the_month() =>
		Assert.Equal([new MonthKey(2026, 5)], new MonthKey(2026, 5).RunEndingHere(1));

	[Theory]
	[InlineData(1)]
	[InlineData(6)]
	[InlineData(24)]
	public void Accepts_run_lengths_in_range(int months)
	{
		Assert.True(MonthKey.TryValidateRun("2026-03", months, out MonthKey end, out _));
		Assert.Equal(new MonthKey(2026, 3), end);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(25)]
	[InlineData(-1)]
	public void Rejects_run_lengths_out_of_range(int months) =>
		Assert.False(MonthKey.TryValidateRun("2026-03", months, out _, out _));

	[Fact]
	public void Rejects_an_invalid_end_month() =>
		Assert.False(MonthKey.TryValidateRun("2026-13", 6, out _, out _));

	[Fact]
	public void An_omitted_end_month_is_the_current_danish_month()
	{
		Assert.True(MonthKey.TryValidateRun(null, 6, out MonthKey end, out _));
		Assert.Equal(MonthKey.Current(), end);
	}

	[Fact]
	public void Resolve_rejects_garbage_and_accepts_omission()
	{
		Assert.False(MonthKey.TryResolve("nope", "month", out _, out _));
		Assert.True(MonthKey.TryResolve(null, "month", out MonthKey month, out _));
		Assert.Equal(MonthKey.Current(), month);
	}

	[Fact]
	public void Orders_by_calendar()
	{
		Assert.True(new MonthKey(2025, 12).CompareTo(new MonthKey(2026, 1)) < 0);
		Assert.True(new MonthKey(2026, 2).CompareTo(new MonthKey(2026, 1)) > 0);
	}

	[Fact]
	public void Of_buckets_an_instant_by_danish_month() =>
		Assert.Equal(new MonthKey(2026, 8), MonthKey.Of(new DateTime(2026, 7, 31, 22, 30, 0, DateTimeKind.Utc)));
}
