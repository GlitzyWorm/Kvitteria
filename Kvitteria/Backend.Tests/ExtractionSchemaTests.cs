using System.Text.RegularExpressions;
using Backend.Features.Extraction;
using Backend.Features.Extraction.LLM;
namespace Backend.Tests;

public class ExtractionSchemaTests
{
	private static string AmountPattern() =>
		ReceiptExtractionPrompt.Schema.GetProperty("properties").GetProperty("grand_total").GetProperty("pattern").GetString()!;

	[Theory]
	[InlineData("29,95")]
	[InlineData("14,95-")]
	[InlineData("-50,00")]
	[InlineData("1.234,50")]
	public void The_pattern_admits_what_the_parser_reads(string amount)
	{
		Assert.Matches(AmountPattern(), amount);
		Assert.True(DanishAmount.TryParse(amount, out _));
	}

	[Theory]
	[InlineData("")]
	[InlineData("2 x 10,05")]
	[InlineData("29")]
	public void The_pattern_refuses_what_is_not_an_amount(string amount) =>
		Assert.False(Regex.IsMatch(amount, AmountPattern()));

	[Fact]
	public void Rows_come_before_the_grand_total()
	{
		string[] names = ReceiptExtractionPrompt.Schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray();

		Assert.True(Array.IndexOf(names, "rows") < Array.IndexOf(names, "grand_total"));
	}
}
