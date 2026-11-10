using System.Text.RegularExpressions;
using MimeKit;
namespace Backend.Features.Mail;

/// <summary>
/// The plain text of a mail as extraction reads it, shared by the poller and the evaluation tool so a
/// sample exported as <c>.eml</c> is read the way the same mail is in production.
/// </summary>
internal static partial class MailBody
{
	/// <summary>The text part, else the HTML part converted; <paramref name="source"/> says which, or <c>none</c>.</summary>
	public static string From(MimeMessage message, out string source)
	{
		if (!string.IsNullOrWhiteSpace(message.TextBody))
		{
			source = "text";
			return NormalizeWhitespace(message.TextBody);
		}

		if (!string.IsNullOrWhiteSpace(message.HtmlBody))
		{
			source = "html";
			return NormalizeWhitespace(HtmlToPlainText.Convert(message.HtmlBody));
		}

		source = "none";
		return "";
	}

	public static string NormalizeWhitespace(string text)
	{
		string[] lines = text.ReplaceLineEndings("\n").Split('\n');
		string joined = string.Join('\n', lines.Select(l => l.TrimEnd()));
		return ExcessNewlines().Replace(joined, "\n\n").Trim();
	}

	[GeneratedRegex(@"\n{3,}")]
	private static partial Regex ExcessNewlines();
}
