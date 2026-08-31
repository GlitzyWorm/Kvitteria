// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.ComponentModel;
using MobileApp.Features.Overview;
using MobileApp.Features.Shared;
using MobileApp.Localization;

namespace MobileApp;

public partial class AppShell : Shell
{
    private readonly ILocalizationService _localization;

    public AppShell(ILocalizationService localization)
    {
        InitializeComponent();

        _localization = localization;
        _localization.PropertyChanged += OnLocalizationChanged;
        ApplyTabTitles();

        Routing.RegisterRoute(AppRoutes.Settings, typeof(SettingsPage));
    }

    /// <summary>
    /// A Tab's Title cannot be reached by the localize markup extension the pages use, so the five
    /// titles are set here instead — and set again whenever the language changes, since the Shell
    /// outlives every page and would otherwise keep the language the app started in.
    /// </summary>
    private void ApplyTabTitles()
    {
        OverviewTab.Title = _localization["TabOverview"];
        SortTab.Title = _localization["TabSort"];
        ReceiptsTab.Title = _localization["TabReceipts"];
        InsightsTab.Title = _localization["TabInsights"];
        PipelineTab.Title = _localization["TabPipeline"];
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) => ApplyTabTitles();
}
