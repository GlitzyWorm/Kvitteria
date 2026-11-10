using System.Globalization;
using Backend.Features.Shared;
namespace Backend.Features.Persistence.Factory;

/// <summary>Converts the model's Copenhagen-local purchase date into UTC.</summary>
public static class PurchaseDate
{
	/// <summary>
	/// Accepted formats: the one ReceiptExtractionPrompt asks for, plus the variants the model
	/// drifts to.
	/// </summary>
	private static readonly string[] Formats = [
		"yyyy-MM-dd HH:mm:ss",
		"yyyy-MM-ddTHH:mm:ss",
		"yyyy-MM-dd HH:mm",
		"yyyy-MM-dd"
	];

	/// <summary>
	/// A purchase may be photographed or opened well after it happened, but not before it happened, so the
	/// window is wide on the past side and one day on the future side (clock and time-zone slack).
	/// </summary>
	private static readonly TimeSpan MaxAfterSource = TimeSpan.FromDays(1);
	private static readonly TimeSpan MaxBeforeSource = TimeSpan.FromDays(365);

	/// <summary>
	/// Parses the extracted date, falling back to the source's own time when it is unparseable or
	/// implausible against it.
	/// </summary>
	public static DateTime Resolve(string extracted, DateTimeOffset mailDate, ILogger logger, string subject)
	{
		if (TryParseLocal(extracted, out DateTime localDate))
		{
			DateTime utc = ToUtc(localDate);
			DateTime source = mailDate.UtcDateTime;

			if (utc <= source + MaxAfterSource && utc >= source - MaxBeforeSource)
				return utc;

			logger.LogWarning(
				"Implausible purchase date '{PurchaseDate}' for '{Subject}'; falling back to the source date {MailDate}.",
				extracted, subject, mailDate);

			return source;
		}

		logger.LogWarning(
			"Unparseable purchase date '{PurchaseDate}' for '{Subject}'; falling back to the mail date {MailDate}.",
			extracted, subject, mailDate);

		return mailDate.UtcDateTime;
	}

	public static bool TryParseLocal(string extracted, out DateTime localDate) =>
		DateTime.TryParseExact(extracted, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out localDate);

	public static DateTime ToUtc(DateTime localDate) => DanishTime.ToUtc(localDate);
}
