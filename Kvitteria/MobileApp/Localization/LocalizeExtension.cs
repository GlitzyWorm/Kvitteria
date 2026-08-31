// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Globalization;
using Microsoft.Maui.Controls.Xaml;

namespace MobileApp.Localization;

/// <summary>
/// Reads a string in XAML: <c>Text="{loc:Localize SignIn}"</c>.
/// </summary>
/// <remarks>
/// Produces a <see cref="Binding"/> rather than a value. A value would be resolved once, at parse
/// time, and the language switch would leave every already-built page in the old language until it
/// was navigated away from and back.
/// </remarks>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class LocalizeExtension : IMarkupExtension<BindingBase>
{
	public string Key { get; set; } = string.Empty;

	/// <summary>
	/// Optional composite format applied to the resolved string, for the few places a value has to
	/// be folded in — <c>{loc:Localize Key=GreetingMorning, Format={}{0}, Mathias}</c>.
	/// </summary>
	public string? StringFormat { get; set; }

	public BindingBase ProvideValue(IServiceProvider serviceProvider) => new Binding
	{
		// The indexer on ILocalizationService. Raising PropertyChanged("Item") — no brackets, see
		// LocalizationService.IndexerName — re-evaluates every one of these at once, which is what
		// makes the switch instant.
		Path = $"[{Key}]",
		Mode = BindingMode.OneWay,
		Source = LocalizationProvider.Current,
		StringFormat = StringFormat
	};

	object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
		=> ProvideValue(serviceProvider);
}

/// <summary>
/// The one <see cref="ILocalizationService"/> XAML binds to.
/// </summary>
/// <remarks>
/// A markup extension is constructed by the XAML parser, not by the container, so it cannot be
/// injected. This is the seam where that is admitted rather than smuggled in as a general service
/// locator: <see cref="MauiProgram"/> sets it once from the resolved singleton, and nothing else
/// reads it.
/// </remarks>
public static class LocalizationProvider
{
	private static ILocalizationService? _current;

	public static ILocalizationService Current
		=> _current ?? throw new InvalidOperationException(
			$"{nameof(LocalizationProvider)} was read before {nameof(Initialize)} ran.");

	public static void Initialize(ILocalizationService service) => _current = service;

	/// <summary>Reads a string from code, where the markup extension is not available.</summary>
	public static string Get(string key) => Current[key];

	/// <summary>Reads a string and folds arguments into it.</summary>
	public static string Get(string key, params object?[] arguments)
		=> string.Format(CultureInfo.CurrentCulture, Current[key], arguments);
}
