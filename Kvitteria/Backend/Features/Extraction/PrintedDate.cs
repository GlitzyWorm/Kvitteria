using System.Text.RegularExpressions;
namespace Backend.Features.Extraction;

/// <summary>
/// Finds the purchase date printed in a receipt's text. The model's own date was wrong whenever register
/// numbers stood in front of it, and a pattern over the text is never worse than a copy of the text.
/// </summary>
public static partial class PrintedDate
{
	private static readonly string[] Months = [
		"januar", "februar", "marts", "april", "maj", "juni",
		"juli", "august", "september", "oktober", "november", "december"
	];

	/// <summary>
	/// The first date with a time beside it, otherwise the first date, as a Copenhagen-local instant with
	/// unspecified kind. A date with no time is midnight. Null when the text prints no valid date.
	/// </summary>
	public static DateTime? Find(string text)
	{
		DateTime? firstWithoutTime = null;

		// Every starting position is tried, not only the one after a rejected match: the register numbers in
		// "13 1 96 08 11 26 09:05" would otherwise swallow the real date.
		for (int start = 0; start < text.Length; start++)
		{
			if (TryReadAt(text, start, out DateTime date, out bool hasTime))
			{
				if (hasTime)
					return date;

				firstWithoutTime ??= date;
			}
		}

		return firstWithoutTime;
	}

	private static bool TryReadAt(string text, int start, out DateTime date, out bool hasTime)
	{
		date = default;
		hasTime = false;

		Match match = WrittenMonth().Match(text, start);

		if (match.Success)
		{
			int month = Array.IndexOf(Months, match.Groups["month"].Value.ToLowerInvariant()) + 1;
			return Build(match, int.Parse(match.Groups["year"].Value), month, out date, out hasTime);
		}

		match = Numeric().Match(text, start);

		if (match.Success)
		{
			int year = int.Parse(match.Groups["year"].Value);

			if (match.Groups["year"].Length == 2)
				year += 2000;

			return Build(match, year, int.Parse(match.Groups["month"].Value), out date, out hasTime);
		}

		return false;
	}

	private static bool Build(Match match, int year, int month, out DateTime date, out bool hasTime)
	{
		date = default;
		hasTime = false;
		int day = int.Parse(match.Groups["day"].Value);

		if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
			return false;

		int hour = 0, minute = 0, second = 0;

		if (match.Groups["hour"].Success)
		{
			hour = int.Parse(match.Groups["hour"].Value);
			minute = int.Parse(match.Groups["minute"].Value);
			second = match.Groups["second"].Success ? int.Parse(match.Groups["second"].Value) : 0;
			hasTime = hour <= 23 && minute <= 59 && second <= 59;

			if (!hasTime)
				hour = minute = second = 0;
		}

		date = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
		return true;
	}

	private const string TimePart = @"(?:[ \t\-–,]{0,6}(?<hour>\d{1,2}):(?<minute>\d{2})(?::(?<second>\d{2}))?)?";

	[GeneratedRegex(
		@"\G(?<!\d)(?<day>\d{1,2})\.?[ \t]+(?<month>januar|februar|marts|april|maj|juni|juli|august|september|oktober|november|december)[ \t]+(?<year>\d{4})(?!\d)" + TimePart,
		RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
	private static partial Regex WrittenMonth();

	[GeneratedRegex(
		@"\G(?<!\d)(?<day>\d{1,2})(?<sep>[.\- ])(?<month>\d{1,2})\k<sep>(?<year>\d{4}|\d{2})(?!\d)" + TimePart,
		RegexOptions.CultureInvariant)]
	private static partial Regex Numeric();
}
