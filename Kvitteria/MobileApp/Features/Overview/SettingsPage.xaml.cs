// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using MobileApp.Features.Overview.SettingViews;
using MobileApp.Localization;
using System.ComponentModel;

namespace MobileApp.Features.Overview;

public partial class SettingsPage : ContentPage
{
    public static readonly BindableProperty SelectedViewModelIndexProperty = BindableProperty.Create(
        nameof(SelectedViewModelIndex), typeof(int), typeof(SettingsPage), 0,
        propertyChanged: (bindable, _, _) => ((SettingsPage)bindable).OnPropertyChanged(nameof(CurrentViewModel)));

    public int SelectedViewModelIndex
    {
        get => (int)GetValue(SelectedViewModelIndexProperty);
        set => SetValue(SelectedViewModelIndexProperty, value);
    }

    private readonly object[] _viewModels;

    public object CurrentViewModel => _viewModels[SelectedViewModelIndex];

    private readonly ILocalizationService _localization;

    public SettingsPage(ILocalizationService localization, AccountTabViewModel accountViewModel, InboxTabViewModel inboxViewModel)
    {
        _localization = localization;
        _localization.PropertyChanged += OnLocalizationChanged;
        ApplyTitle();

        _viewModels = [accountViewModel, inboxViewModel];

        InitializeComponent();
    }

    private void ApplyTitle()
    {
        Title = _localization["SettingsTitle"];
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) => ApplyTitle();
}
