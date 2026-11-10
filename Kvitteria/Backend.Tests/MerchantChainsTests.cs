using Backend.Features.Extraction;
namespace Backend.Tests;

public class MerchantChainsTests
{
	[Theory]
	[InlineData("Netto Scan&Go\nKvittering", "Netto")]
	[InlineData("Super Brugsen\nVarer", "SuperBrugsen")]
	[InlineData("SUPERBRUGSEN BRØNDBY", "SuperBrugsen")]
	[InlineData("www.føtex.dk\nSALLING RISNUDLER 12,00", "Føtex")]
	[InlineData("REMA1000 Brøndby", "Rema 1000")]
	public void Matches_a_known_chain(string text, string expected) =>
		Assert.Equal(expected, MerchantChains.Match(text));

	[Theory]
	[InlineData("COOP BONUSKONTO 12,00")]
	[InlineData("Du har sparet 12,00")]
	[InlineData("Brødrene Hansens købmand")]
	[InlineData("")]
	public void Matches_nothing_otherwise(string text) =>
		Assert.Null(MerchantChains.Match(text));

	[Fact]
	public void The_earliest_form_in_the_text_wins() =>
		Assert.Equal("Føtex", MerchantChains.Match("FØTEX\nNETTO-pose 1,00"));
}
