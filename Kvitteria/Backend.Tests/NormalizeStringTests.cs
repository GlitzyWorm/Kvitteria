using System.Globalization;
using Backend.Features.Shared;
namespace Backend.Tests;

public class NormalizeStringTests
{
	[Theory]
	[InlineData("  HK. OKSE 8-12%  ", "hk. okse 8-12%")]
	[InlineData("Mælk", "mælk")]
	[InlineData("RØDGRØD MED FLØDE", "rødgrød med fløde")]
	[InlineData("Æbler", "æbler")]
	public void Trims_and_lowercases(string raw, string expected) =>
		Assert.Equal(expected, raw.NormalizeString());

	/// <summary>
	/// The normalized name is the key stored in ItemAlias and joined against on ingestion, so it has to
	/// be culture-independent. Under a Turkish culture a culture-sensitive ToLower maps 'I' to a
	/// dotless 'ı', which silently breaks every alias lookup containing an I.
	/// </summary>
	[Fact]
	public void Is_independent_of_the_current_culture()
	{
		CultureInfo original = CultureInfo.CurrentCulture;

		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
			string turkish = "KILDEVAND".NormalizeString();

			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			string invariant = "KILDEVAND".NormalizeString();

			Assert.Equal("kildevand", invariant);
			Assert.Equal(invariant, turkish);
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}
}
