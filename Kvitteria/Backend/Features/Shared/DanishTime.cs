namespace Backend.Features.Shared;

/// <summary>
/// Copenhagen wall-clock time. Receipts print local time and "this month" means the Danish calendar
/// month, neither of which lines up with UTC.
/// </summary>
public static class DanishTime
{
	private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

	public static DateTime ToUtc(DateTime localDate)
	{
		// A time inside the spring-forward gap (02:30 on the last Sunday in March) never existed, and
		// ConvertTimeToUtc throws on it. Nudge it past the transition.
		if (Zone.IsInvalidTime(localDate))
			localDate = localDate.AddHours(1);

		return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified), Zone);
	}

	/// <summary>
	/// The UTC instants bounding the Danish calendar month containing <paramref name="now"/>: inclusive
	/// start, exclusive end.
	/// </summary>
	public static (DateTime StartUtc, DateTime EndUtc) MonthRangeUtc(DateTimeOffset now)
	{
		DateTime local = TimeZoneInfo.ConvertTime(now, Zone).DateTime;
		var start = new DateTime(local.Year, local.Month, 1);

		return (ToUtc(start), ToUtc(start.AddMonths(1)));
	}

	/// <summary>
	/// The UTC instants bounding the given Danish calendar month: inclusive start, exclusive end.
	/// </summary>
	public static (DateTime StartUtc, DateTime EndUtc) MonthRangeUtc(int year, int month)
	{
		var start = new DateTime(year, month, 1);

		return (ToUtc(start), ToUtc(start.AddMonths(1)));
	}

	/// <summary>The Danish calendar month an instant falls in.</summary>
	public static (int Year, int Month) MonthOf(DateTime utc)
	{
		DateTime local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

		return (local.Year, local.Month);
	}
}
