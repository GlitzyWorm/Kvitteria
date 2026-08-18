using System.Globalization;
namespace Backend.Features.Persistence.Factory;

/// <summary>
/// Converts the model's Copenhagen-local purchase date into UTC. Pure and side-effect free apart
/// from the fallback log, so the parsing rules are directly testable.
/// </summary>
public static class PurchaseDate
{
	private static readonly TimeZoneInfo DanishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

	/// <summary>
	/// The format ReceiptExtractionPrompt asks for, plus the variants the model drifts to. Parsing is
	/// exact-with-a-fallback rather than DateTime.Parse, because this is unvalidated model output and
	/// a FormatException here used to kill the receipt factory outright.
	/// </summary>
	private static readonly string[] Formats = [
		"yyyy-MM-dd HH:mm:ss",
		"yyyy-MM-ddTHH:mm:ss",
		"yyyy-MM-dd HH:mm",
		"yyyy-MM-dd"
	];

	/// <summary>Parses the extracted date, falling back to the mail's own timestamp.</summary>
	public static DateTime Resolve(string extracted, DateTimeOffset mailDate, ILogger logger, string subject)
	{
		if (TryParseLocal(extracted, out DateTime localDate))
			return ToUtc(localDate);

		logger.LogWarning(
			"Unparseable purchase date '{PurchaseDate}' for '{Subject}'; falling back to the mail date {MailDate}.",
			extracted, subject, mailDate);

		return mailDate.UtcDateTime;
	}

	public static bool TryParseLocal(string extracted, out DateTime localDate) =>
		DateTime.TryParseExact(extracted, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out localDate);

	public static DateTime ToUtc(DateTime localDate)
	{
		// A receipt stamped inside the spring-forward gap (02:30 on the last Sunday in March) names a
		// wall-clock time that never existed, and ConvertTimeToUtc throws on it. Nudge it past the
		// transition instead of failing the whole receipt.
		if (DanishTimeZone.IsInvalidTime(localDate))
			localDate = localDate.AddHours(1);

		return TimeZoneInfo.ConvertTimeToUtc(localDate, DanishTimeZone);
	}
}
