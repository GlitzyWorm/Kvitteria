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

	public static DateTime ToUtc(DateTime localDate) => DanishTime.ToUtc(localDate);
}
