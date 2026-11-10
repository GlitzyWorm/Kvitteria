using Backend.Features.Extraction;
namespace Backend.Tests;

public class PrintedDateTests
{
	[Theory]
	[InlineData("11 1 462 26 10 26 13:07", 2026, 10, 26, 13, 7, 0)]
	[InlineData("13 1 96 08 11 26 09:05", 2026, 11, 8, 9, 5, 0)]
	[InlineData("31.10.2026 11:40", 2026, 10, 31, 11, 40, 0)]
	[InlineData("9. november 2026 - 13:33\nTidspunkt: 09-11-2026 13:33:17", 2026, 11, 9, 13, 33, 0)]
	[InlineData("Tidspunkt: 09-11-2026 13:33:17", 2026, 11, 9, 13, 33, 17)]
	[InlineData("01 11 2026", 2026, 11, 1, 0, 0, 0)]
	public void Finds_the_printed_date(string text, int y, int mo, int d, int h, int mi, int s) =>
		Assert.Equal(new DateTime(y, mo, d, h, mi, s), PrintedDate.Find(text));

	[Fact]
	public void Prefers_a_date_with_a_time_over_an_earlier_bare_one() =>
		Assert.Equal(new DateTime(2026, 11, 8, 9, 5, 0), PrintedDate.Find("01 01 2026\n08 11 26 09:05"));

	[Theory]
	[InlineData("31 02 26")]
	[InlineData("MÆLK 12,95\nI ALT 12,95")]
	[InlineData("")]
	public void Finds_nothing_in_invalid_or_dateless_text(string text) =>
		Assert.Null(PrintedDate.Find(text));
}
