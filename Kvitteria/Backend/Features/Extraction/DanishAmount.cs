using System.Globalization;
using System.Text.RegularExpressions;
namespace Backend.Features.Extraction;

/// <summary>
/// Reads an amount as a Danish receipt prints it: a decimal comma, an optional thousands dot, and a minus
/// before or after. Anything else is rejected, so a quantity line or an empty value is not mistaken for
/// money.
/// </summary>
public static partial class DanishAmount
{
	public static bool TryParse(string? text, out decimal value)
	{
		value = 0;

		if (text is null)
			return false;

		Match match = AmountPattern().Match(text);

		if (!match.Success)
			return false;

		bool leading = match.Groups["lead"].Success;
		bool trailing = match.Groups["trail"].Success;

		// A minus on both sides is not a printed amount.
		if (leading && trailing)
			return false;

		string digits = match.Groups["int"].Value.Replace(".", "", StringComparison.Ordinal);
		decimal magnitude = decimal.Parse($"{digits}.{match.Groups["frac"].Value}", NumberStyles.Number, CultureInfo.InvariantCulture);
		value = leading || trailing ? -magnitude : magnitude;
		return true;
	}

	[GeneratedRegex(@"^(?<lead>-)?(?<int>\d{1,3}(?:\.\d{3})+|\d+),(?<frac>\d{2})(?<trail>-)?$", RegexOptions.CultureInvariant)]
	private static partial Regex AmountPattern();
}
