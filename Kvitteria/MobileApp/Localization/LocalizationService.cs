// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace MobileApp.Localization;

/// <summary>
/// Every user-facing string, and the language they are read in. An indexer rather than generated
/// properties because it has to raise change notifications: the language is switchable at runtime,
/// and a static lookup would leave the previous language on screen until each page was rebuilt.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
	/// <summary>The string for <paramref name="key"/>, or the key itself when it is missing.</summary>
	string this[string key] { get; }

	CultureInfo Culture { get; }

	/// <summary>The languages this app ships. Danish first: it is the neutral resource set.</summary>
	IReadOnlyList<CultureInfo> SupportedCultures { get; }

	void SetCulture(CultureInfo culture);
}

/// <inheritdoc />
public sealed class LocalizationService : ILocalizationService
{
	private const string PreferenceKey = "app.culture";

	/// <summary>
	/// The indexer's property name, raised with no brackets. MAUI's <c>BindingExpression</c> does not
	/// honour WPF's "Item[]" wildcard — a name containing "[" must match the binding's exact key
	/// (<c>Item[Account]</c>) to refresh. Only the bare name, with no brackets at all, matches every
	/// indexer binding regardless of key, which is what re-reads every localized string in one go.
	/// </summary>
	private const string IndexerName = "Item";

	/// <summary>
	/// Danish is the <em>neutral</em> resource set rather than English. The app is Danish-first, so
	/// an unrecognised system language should fall back to Danish; making English neutral would
	/// invert that for every locale nobody has translated.
	/// </summary>
	public static readonly CultureInfo Danish = new("da");

	public static readonly CultureInfo English = new("en");

	private readonly ResourceManager _resources = new(typeof(AppResources).FullName!, typeof(AppResources).Assembly);

	public LocalizationService()
	{
		string stored = Preferences.Default.Get(PreferenceKey, string.Empty);

		Culture = stored.Length > 0
			? Resolve(stored)
			: Resolve(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

		Apply(Culture);
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public CultureInfo Culture { get; private set; }

	public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [Danish, English];

	public string this[string key]
	{
		get
		{
			if (string.IsNullOrEmpty(key))
			{
				return string.Empty;
			}

			// Falling back to the key makes a missing string obvious on screen rather than blank,
			// which is the difference between noticing it in review and shipping it.
			return _resources.GetString(key, Culture) ?? key;
		}
	}

	public void SetCulture(CultureInfo culture)
	{
		CultureInfo resolved = Resolve(culture.TwoLetterISOLanguageName);

		if (Equals(resolved, Culture))
		{
			return;
		}

		Culture = resolved;
		Preferences.Default.Set(PreferenceKey, resolved.Name);
		Apply(resolved);

		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
	}

	private static CultureInfo Resolve(string twoLetterOrName)
		=> twoLetterOrName.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? English : Danish;

	/// <summary>
	/// Sets the thread cultures too, not only the resource lookup: dates and amounts elsewhere in
	/// the app are formatted from <see cref="CultureInfo.CurrentCulture"/>, and a Danish UI showing
	/// dollar-style decimal points would be its own bug.
	/// </summary>
	private static void Apply(CultureInfo culture)
	{
		CultureInfo.DefaultThreadCurrentCulture = culture;
		CultureInfo.DefaultThreadCurrentUICulture = culture;
		CultureInfo.CurrentCulture = culture;
		CultureInfo.CurrentUICulture = culture;
	}
}
