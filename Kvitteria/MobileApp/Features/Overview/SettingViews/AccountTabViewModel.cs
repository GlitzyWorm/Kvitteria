// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.Input;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using MobileApp.Services;
using System.Globalization;

namespace MobileApp.Features.Overview.SettingViews;

public sealed partial class AccountTabViewModel(
    ISessionService sessions,
    IServerSettings serverSettings,
    INavigator navigator,
    ILocalizationService localization) : ViewModelBase(localization)
{
    public string ServerDisplay => serverSettings.BaseUrl ?? string.Empty;

    public string SignedInAs => sessions.Session?.Email ?? string.Empty;

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

    private bool IsEnglishCulture =>
        Localization.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (!await ConfirmAsync("ConfirmSignOutTitle", "ConfirmSignOutMessage", "SignOut"))
        {
            return;
        }

        await sessions.SignOutAsync();
        await navigator.GoToAsync(AppRoutes.Gate);
    }

    [RelayCommand]
    private async Task ChangeServerAsync()
    {
        if (!await ConfirmAsync("ConfirmChangeServerTitle", "ConfirmChangeServerMessage", "ChangeServer"))
        {
            return;
        }

        await sessions.SignOutAsync();
        serverSettings.Clear();

        await navigator.GoToAsync(AppRoutes.Server);
    }

    private void SetCulture(CultureInfo culture)
    {
        Localization.SetCulture(culture);
        OnPropertyChanged(nameof(IsDanish));
        OnPropertyChanged(nameof(IsEnglish));
    }

    private Task<bool> ConfirmAsync(string titleKey, string messageKey, string acceptKey) =>
        navigator.ConfirmAsync(Localization[titleKey], Localization[messageKey], Localization[acceptKey],
            Localization["Cancel"]);
}