using System.Text;
using MimeKit.Text;
namespace Backend.Features.Mail;

/// <summary>
/// Converts receipt mails that ship no plain-text alternative. Block structure becomes newlines and
/// table cells become tabs; script, style and head content is dropped.
/// </summary>
internal static class HtmlToPlainText
{
	public static string Convert(string html)
	{
		var text = new StringBuilder();
		var tokenizer = new HtmlTokenizer(new StringReader(html)) { DecodeCharacterReferences = true };
		var skipContent = false;

		while (tokenizer.ReadNextToken(out HtmlToken? token))
		{
			switch (token)
			{
				case HtmlTagToken tag:
					if (tag.Id is HtmlTagId.Script or HtmlTagId.Style or HtmlTagId.Head)
					{
						skipContent = !tag.IsEndTag;
					}
					else if (IsBlockLevel(tag.Id))
					{
						text.Append('\n');
					}
					else if (tag.Id == HtmlTagId.TD)
					{
						text.Append('\t');
					}
					break;

				case HtmlDataToken data when !skipContent:
					text.Append(data.Data);
					break;
			}
		}

		return text.ToString();
	}

	private static bool IsBlockLevel(HtmlTagId id) => id is
		HtmlTagId.P or HtmlTagId.Div or HtmlTagId.Br or HtmlTagId.TR or HtmlTagId.Table or
		HtmlTagId.LI or HtmlTagId.UL or HtmlTagId.OL or HtmlTagId.H1 or HtmlTagId.H2 or
		HtmlTagId.H3 or HtmlTagId.H4 or HtmlTagId.H5 or HtmlTagId.H6 or HtmlTagId.HR or
		HtmlTagId.BlockQuote or HtmlTagId.Section or HtmlTagId.Article or HtmlTagId.Header or
		HtmlTagId.Footer;
}
