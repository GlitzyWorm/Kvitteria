using System.Globalization;
using Backend.Features.Shared;
namespace Backend.Features.Insights;

/// <summary>A Danish calendar month, written <c>yyyy-MM</c> on the wire.</summary>
public readonly record struct MonthKey(int Year, int Month) : IComparable<MonthKey>
{
	public const int MinRun = 1;
	public const int MaxRun = 24;
	public const int DefaultRun = 6;

	private const int MinYear = 1900;
	private const int MaxYear = 2999;

	public static MonthKey Of(DateTime utc)
	{
		(int year, int month) = DanishTime.MonthOf(utc);
		return new MonthKey(year, month);
	}

	public static MonthKey Current() => Of(DateTime.UtcNow);

	/// <summary>Strictly <c>yyyy-MM</c>: no trimming, no single-digit month, no day.</summary>
	public static bool TryParse(string? value, out MonthKey month)
	{
		month = default;

		if (value is not { Length: 7 } || value[4] != '-')
			return false;

		if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out int year)
		    || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int m))
			return false;

		if (year is < MinYear or > MaxYear || m is < 1 or > 12)
			return false;

		month = new MonthKey(year, m);
		return true;
	}

	public MonthKey AddMonths(int months)
	{
		int index = Year * 12 + (Month - 1) + months;
		return new MonthKey(index / 12, index % 12 + 1);
	}

	public MonthKey Previous() => AddMonths(-1);

	/// <summary>Inclusive start, exclusive end, in UTC.</summary>
	public (DateTime StartUtc, DateTime EndUtc) RangeUtc() => DanishTime.MonthRangeUtc(Year, Month);

	/// <summary>The <paramref name="count"/> months ending at this one, oldest first.</summary>
	public List<MonthKey> RunEndingHere(int count)
	{
		MonthKey end = this;
		return Enumerable.Range(0, count).Select(i => end.AddMonths(i - (count - 1))).ToList();
	}

	/// <summary>
	/// Resolves an optional month parameter: omitted means the current Danish month, anything that is not
	/// <c>yyyy-MM</c> is a 400.
	/// </summary>
	public static bool TryResolve(string? value, string parameterName, out MonthKey month, out IResult problem)
	{
		if (string.IsNullOrEmpty(value))
		{
			month = Current();
			problem = Results.Empty;
			return true;
		}

		if (TryParse(value, out month))
		{
			problem = Results.Empty;
			return true;
		}

		problem = Results.Problem(
			statusCode: StatusCodes.Status400BadRequest,
			detail: $"{parameterName} must be a month written yyyy-MM, for example 2026-03.");
		return false;
	}

	/// <summary>Validates an end month and a run length, as <see cref="Paging.TryValidate"/> does for paging.</summary>
	public static bool TryValidateRun(string? endMonth, int months, out MonthKey end, out IResult problem)
	{
		if (!TryResolve(endMonth, "endMonth", out end, out problem))
			return false;

		if (months is < MinRun or > MaxRun)
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: $"months must be between {MinRun} and {MaxRun}.");
			return false;
		}

		problem = Results.Empty;
		return true;
	}

	public int CompareTo(MonthKey other) => (Year * 12 + Month).CompareTo(other.Year * 12 + other.Month);

	public override string ToString() => $"{Year:0000}-{Month:00}";
}
