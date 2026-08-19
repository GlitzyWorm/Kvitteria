using Backend.Features.Mail;
namespace Backend.Tests;

public class HtmlToPlainTextTests
{
	[Fact]
	public void Keeps_block_structure_as_line_breaks()
	{
		string text = HtmlToPlainText.Convert("<p>NETTO</p><p>I ALT</p>");

		Assert.Contains("NETTO", text, StringComparison.Ordinal);
		Assert.Contains("I ALT", text, StringComparison.Ordinal);
		Assert.Contains("\n", text, StringComparison.Ordinal);
	}

	[Fact]
	public void Separates_table_cells_so_amounts_do_not_run_into_names()
	{
		string text = HtmlToPlainText.Convert("<table><tr><td>MINIMAELK</td><td>12,50</td></tr></table>");

		Assert.DoesNotContain("MINIMAELK12,50", text, StringComparison.Ordinal);
		Assert.Contains("MINIMAELK", text, StringComparison.Ordinal);
		Assert.Contains("12,50", text, StringComparison.Ordinal);
	}

	[Fact]
	public void Drops_script_style_and_head_content()
	{
		string text = HtmlToPlainText.Convert(
			"<head><title>ignored</title></head><style>.x{color:red}</style>" +
			"<script>var a = 1;</script><p>HERAF PANT IALT</p>");

		Assert.Contains("HERAF PANT IALT", text, StringComparison.Ordinal);
		Assert.DoesNotContain("ignored", text, StringComparison.Ordinal);
		Assert.DoesNotContain("color:red", text, StringComparison.Ordinal);
		Assert.DoesNotContain("var a", text, StringComparison.Ordinal);
	}

	[Fact]
	public void Decodes_character_references_so_danish_text_survives()
	{
		string text = HtmlToPlainText.Convert("<p>R&oslash;dgr&oslash;d &amp; Fl&oslash;de</p>");

		Assert.Contains("Rødgrød & Fløde", text, StringComparison.Ordinal);
	}

	[Fact]
	public void Preserves_the_decimal_comma_used_on_danish_receipts()
	{
		string text = HtmlToPlainText.Convert("<div>I ALT 123,45</div>");

		Assert.Contains("123,45", text, StringComparison.Ordinal);
	}

	[Fact]
	public void Returns_empty_for_empty_input() =>
		Assert.Equal("", HtmlToPlainText.Convert(""));
}
