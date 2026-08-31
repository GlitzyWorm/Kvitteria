// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Shared;

/// <summary>
/// The one or two letters standing in for a household member's face, as the source's own
/// <c>initials(name)</c> does.
/// </summary>
public static class Initials
{
	/// <summary>What to show when there is no name to work from.</summary>
	public const string Unknown = "?";

	/// <summary>
	/// Up to two uppercase letters: the first of the first word and the first of the last. Uppercasing
	/// is invariant, because a Turkish device must not turn an "i" into "İ" in a Danish member's name.
	/// </summary>
	public static string From(string? displayName)
	{
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return Unknown;
		}

		string[] words = displayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

		if (words.Length == 0)
		{
			return Unknown;
		}

		// One word gives one letter rather than its first two: "Sofie" is S, not SO — a name is not an
		// abbreviation of itself.
		string first = words[0][..1];

		return words.Length == 1
			? first.ToUpperInvariant()
			: (first + words[^1][..1]).ToUpperInvariant();
	}
}
