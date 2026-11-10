namespace Backend.Features.Extraction;

/// <summary>
/// Names the Danish grocery chain a receipt text prints, with one spelling per chain, so the same shop is
/// never stored as both <c>Super Brugsen</c> and <c>SuperBrugsen</c>. <c>Salling</c> is left out because it
/// is printed as a product brand on Føtex receipts, and <c>Coop</c> alone because <c>COOP BONUSKONTO</c> is
/// printed on SuperBrugsen receipts.
/// </summary>
public static class MerchantChains
{
	private static readonly (string Name, string[] Forms)[] Chains = [
		("Netto", ["netto"]),
		("Rema 1000", ["rema 1000", "rema1000"]),
		("Føtex", ["føtex"]),
		("Bilka", ["bilka"]),
		("Lidl", ["lidl"]),
		("Coop 365", ["coop 365", "365discount"]),
		("Kvickly", ["kvickly"]),
		("SuperBrugsen", ["superbrugsen", "super brugsen"]),
		("Dagli'Brugsen", ["dagli'brugsen", "dagli’brugsen", "dagli brugsen", "daglibrugsen"]),
		("Lokal Brugsen", ["lokal brugsen", "lokalbrugsen"]),
		("Meny", ["meny"]),
		("Spar", ["spar"]),
		("Løvbjerg", ["løvbjerg"])
	];

	/// <summary>The chain whose form stands earliest in the text, or null when none is printed.</summary>
	public static string? Match(string text)
	{
		string lower = text.ToLowerInvariant();
		string? best = null;
		int bestIndex = int.MaxValue;

		foreach ((string name, string[] forms) in Chains)
		{
			foreach (string form in forms)
			{
				int index = FirstBounded(lower, form);

				if (index >= 0 && index < bestIndex)
				{
					best = name;
					bestIndex = index;
				}
			}
		}

		return best;
	}

	/// <summary>Index of the first occurrence with no letter or digit right before or after it, or -1.</summary>
	private static int FirstBounded(string text, string form)
	{
		int from = 0;

		while (from <= text.Length - form.Length)
		{
			int index = text.IndexOf(form, from, StringComparison.Ordinal);

			if (index < 0)
				return -1;

			bool before = index > 0 && IsWordChar(text[index - 1]);
			bool after = index + form.Length < text.Length && IsWordChar(text[index + form.Length]);

			if (!before && !after)
				return index;

			from = index + 1;
		}

		return -1;
	}

	private static bool IsWordChar(char c) =>
		c is >= 'a' and <= 'z' or 'æ' or 'ø' or 'å' or >= '0' and <= '9';
}
