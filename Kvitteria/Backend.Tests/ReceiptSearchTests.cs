using Backend.Features.Receipts;

namespace Backend.Tests;

public class ReceiptSearchTests
{
	private static ReceiptSearchTerm Parse(string? search)
	{
		Assert.True(ReceiptSearch.TryParse(search, out ReceiptSearchTerm? term, out _));
		return term!;
	}

	[Fact]
	public void Trims_the_term() => Assert.Equal("%rema%", Parse("  rema \t").Pattern);

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t\n")]
	public void An_empty_term_succeeds_with_no_filter(string? search)
	{
		Assert.True(ReceiptSearch.TryParse(search, out ReceiptSearchTerm? term, out _));
		Assert.Null(term);
	}

	[Fact]
	public void Accepts_exactly_the_length_limit() =>
		Assert.True(ReceiptSearch.TryParse(new string('a', ReceiptSearch.MaxLength), out _, out _));

	[Fact]
	public void Rejects_a_term_over_the_limit() =>
		Assert.False(ReceiptSearch.TryParse(new string('a', ReceiptSearch.MaxLength + 1), out _, out _));

	[Fact]
	public void The_limit_applies_after_trimming() =>
		Assert.True(ReceiptSearch.TryParse("  " + new string('a', ReceiptSearch.MaxLength) + "  ", out _, out _));

	[Theory]
	[InlineData("50%", @"%50\%%")]
	[InlineData("a_b", @"%a\_b%")]
	[InlineData(@"a\b", @"%a\\b%")]
	[InlineData(@"\%_", @"%\\\%\_%")]
	public void Wildcard_and_escape_characters_are_made_literal(string search, string pattern) =>
		Assert.Equal(pattern, Parse(search).Pattern);

	[Fact]
	public void Keeps_danish_letters_untouched() => Assert.Equal("%føtex%", Parse("føtex").Pattern);

	[Fact]
	public void Text_is_not_an_amount()
	{
		ReceiptSearchTerm term = Parse("rema");

		Assert.Null(term.AmountFrom);
		Assert.Null(term.AmountTo);
	}

	[Theory]
	[InlineData("284,50")]
	[InlineData("284.50")]
	public void A_decimal_amount_matches_exactly_that_amount_whichever_separator(string search)
	{
		ReceiptSearchTerm term = Parse(search);

		Assert.Equal(284.50m, term.AmountFrom);
		Assert.Equal(284.51m, term.AmountTo);
	}

	[Fact]
	public void A_whole_number_spans_up_to_the_next_whole_number()
	{
		ReceiptSearchTerm term = Parse("284");

		Assert.Equal(284m, term.AmountFrom);
		Assert.Equal(285m, term.AmountTo);
	}

	[Fact]
	public void A_number_still_searches_names()
	{
		Assert.Equal("%1000%", Parse("1000").Pattern);
		Assert.Equal(1000m, Parse("1000").AmountFrom);
	}

	[Theory]
	[InlineData("284,")]
	[InlineData(",50")]
	[InlineData("1,000,50")]
	[InlineData("-5")]
	[InlineData("12a")]
	[InlineData("99999999999999999999999999999999")]
	public void Things_that_are_not_plain_numbers_are_text_only(string search) => Assert.Null(Parse(search).AmountFrom);

	[Fact]
	public void The_escape_character_is_the_backslash_the_pattern_is_escaped_with() =>
		Assert.Equal(@"\", ReceiptSearch.EscapeChar);
}
