using Backend.Features.Extraction;
namespace Backend.Tests;

public class DanishAmountTests
{
	[Theory]
	[InlineData("29,95", "29.95")]
	[InlineData("14,95-", "-14.95")]
	[InlineData("-50,00", "-50.00")]
	[InlineData("1.234,50", "1234.50")]
	[InlineData("1234,50", "1234.50")]
	public void Reads_printed_amounts(string text, string expected)
	{
		Assert.True(DanishAmount.TryParse(text, out decimal value));
		Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
	}

	[Theory]
	[InlineData("")]
	[InlineData("2 x 10,05")]
	[InlineData("29")]
	[InlineData("29.95")]
	[InlineData("-14,95-")]
	[InlineData("1.23,50")]
	[InlineData(null)]
	public void Rejects_anything_else(string? text) =>
		Assert.False(DanishAmount.TryParse(text, out _));
}
