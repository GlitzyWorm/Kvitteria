// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Globalization;
using MobileApp.Features.Shared;

namespace MobileApp.Tests;

public class InitialsTests
{
	[Theory]
	[InlineData("Sofie Jelshøj", "SJ")]
	[InlineData("mathias bruun", "MB")]
	[InlineData("Anne Marie Holm", "AH")]
	public void Takes_the_first_and_last_word(string name, string expected)
	{
		Assert.Equal(expected, Initials.From(name));
	}

	/// <summary>One word gives one letter: a name is not an abbreviation of itself.</summary>
	[Theory]
	[InlineData("Sofie", "S")]
	[InlineData("øre", "Ø")]
	public void Takes_one_letter_from_one_word(string name, string expected)
	{
		Assert.Equal(expected, Initials.From(name));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t\n")]
	public void Falls_back_when_there_is_no_name(string? name)
	{
		Assert.Equal(Initials.Unknown, Initials.From(name));
	}

	[Fact]
	public void Ignores_extra_whitespace_between_words()
	{
		Assert.Equal("SJ", Initials.From("  Sofie   Jelshøj  "));
	}

	[Theory]
	[InlineData("Æble Ørsted", "ÆØ")]
	[InlineData("åse Ågesen", "ÅÅ")]
	public void Handles_Danish_letters(string name, string expected)
	{
		Assert.Equal(expected, Initials.From(name));
	}

	/// <summary>
	/// Uppercasing is invariant, so a Turkish device does not turn a Danish member's "i" into "İ" — the
	/// same culture trap NormalizeString guards against on the backend.
	/// </summary>
	[Fact]
	public void Uppercases_independently_of_the_current_culture()
	{
		CultureInfo original = CultureInfo.CurrentCulture;

		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
			Assert.Equal("II", Initials.From("ida ingemann"));
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}
}
