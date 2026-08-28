// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.Input;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using MobileApp.Services;

namespace MobileApp.Features.Overview;

/// <summary>
/// What the control in the top-right corner of the overview offers: leave this account, leave this
/// server, or change language.
/// </summary>
public sealed partial class AccountSheetViewModel(
	ISessionService sessions,
	IServerSettings serverSettings,
	INavigator navigator,
	ILocalizationService localization) : ViewModelBase(localization)
{
	/// <summary>
	/// Dismisses the sheet. Set by the view, because closing a popup is the view's business and the
	/// alternative is this view model holding a Page. Every command that navigates awaits it first:
	/// changing route underneath a still-open popup leaves the popup sitting over the new screen.
	/// </summary>
	public Func<Task>? CloseAsync { get; set; }

	public string ServerDisplay => serverSettings.BaseUrl ?? string.Empty;

	public string SignedInAs => sessions.Session?.Email ?? string.Empty;

	/// <summary>
	/// The language options. Read straight off the localization service rather than mirrored into a
	/// field, so the sheet cannot disagree with the language actually in force; the two are derived
	/// from each other so the segmented control can never show both as on.
	/// </summary>
	public bool IsDanish
	{
		get => !IsEnglishCulture;
		set
		{
			if (value)
			{
				SetCulture(LocalizationService.Danish);
			}
		}
	}

	public bool IsEnglish
	{
		get => IsEnglishCulture;
		set
		{
			if (value)
			{
				SetCulture(LocalizationService.English);
			}
		}
	}

	private bool IsEnglishCulture
		=> Localization.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

	[RelayCommand]
	private async Task SignOutAsync()
	{
		if (!await ConfirmAsync("ConfirmSignOutTitle", "ConfirmSignOutMessage", "SignOut"))
		{
			return;
		}

		await DismissAsync();
		await sessions.SignOutAsync();

		// Back to the gate rather than straight to the sign-in screen: the server may have been
		// claimed, unclaimed or switched off since launch, and the gate is the one thing that knows
		// how to ask.
		await navigator.GoToAsync(AppRoutes.Gate);
	}

	[RelayCommand]
	private async Task ChangeServerAsync()
	{
		if (!await ConfirmAsync("ConfirmChangeServerTitle", "ConfirmChangeServerMessage", "ChangeServer"))
		{
			return;
		}

		await DismissAsync();

		// Both, and in this order. The tokens were issued by that server's GoTrue and mean nothing
		// to another one, so leaving them behind could only ever produce a confusing 401.
		await sessions.SignOutAsync();
		serverSettings.Clear();

		await navigator.GoToAsync(AppRoutes.Server);
	}

	[RelayCommand]
	private Task Dismiss() => DismissAsync();

	private void SetCulture(System.Globalization.CultureInfo culture)
	{
		Localization.SetCulture(culture);
		OnPropertyChanged(nameof(IsDanish));
		OnPropertyChanged(nameof(IsEnglish));
	}

	private Task<bool> ConfirmAsync(string titleKey, string messageKey, string acceptKey)
		=> navigator.ConfirmAsync(
			Localization[titleKey],
			Localization[messageKey],
			Localization[acceptKey],
			Localization["Cancel"]);

	private Task DismissAsync() => CloseAsync?.Invoke() ?? Task.CompletedTask;
}
