// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using MobileApp.Features.Overview.SettingViews;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using System.ComponentModel;

namespace MobileApp.Features.Overview;

public partial class SettingsPage : ContentPage
{
	public static readonly BindableProperty SelectedViewModelIndexProperty = BindableProperty.Create(
		nameof(SelectedViewModelIndex), typeof(int), typeof(SettingsPage), 0,
		propertyChanged: OnSelectedViewModelIndexChanged);

	public int SelectedViewModelIndex
	{
		get => (int)GetValue(SelectedViewModelIndexProperty);
		set => SetValue(SelectedViewModelIndexProperty, value);
	}

	private readonly object[] _viewModels;

	/// <summary>
	/// The selected tab's view model, used only to decide what to refresh. Each tab view binds its own
	/// view model through the typed properties below: binding both to this one handed the Account view an
	/// InboxTabViewModel whenever the Inbox tab was selected, which went unnoticed only because
	/// x:DataType is a compile-time promise and the mismatched bindings quietly resolved to nothing.
	/// </summary>
	public object CurrentViewModel => _viewModels[SelectedViewModelIndex];

	public AccountTabViewModel AccountViewModel { get; }

	public InboxTabViewModel InboxViewModel { get; }

	private readonly ILocalizationService _localization;

	public SettingsPage(ILocalizationService localization, AccountTabViewModel accountViewModel, InboxTabViewModel inboxViewModel)
	{
		_localization = localization;
		_localization.PropertyChanged += OnLocalizationChanged;
		ApplyTitle();

		AccountViewModel = accountViewModel;
		InboxViewModel = inboxViewModel;

		_viewModels = [accountViewModel, inboxViewModel];

		InitializeComponent();
	}

	/// <summary>
	/// Refreshes on every appearance, not once on construction. Shell keeps a page alive for the life of
	/// its route, so a mailbox added on another device — or the sheet on this one — would otherwise never
	/// show up. The same reasoning as GateViewModel resolving on appearing.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		RefreshCurrent();
	}

	private static void OnSelectedViewModelIndexChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var page = (SettingsPage)bindable;
		page.OnPropertyChanged(nameof(CurrentViewModel));

		// Switching to a tab is the other moment its data might be stale.
		page.RefreshCurrent();
	}

	/// <summary>
	/// Asks the selected tab to reload, if it has anything to reload. Fire-and-forget deliberately:
	/// neither OnAppearing nor a property-changed callback can await, and the view model owns its own
	/// busy flag and error state, so there is nothing here to handle.
	/// </summary>
	private void RefreshCurrent()
	{
		if (CurrentViewModel is IRefreshable refreshable)
		{
			_ = refreshable.RefreshAsync(CancellationToken.None);
		}
	}

	private void ApplyTitle()
	{
		Title = _localization["SettingsTitle"];
	}

	private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) => ApplyTitle();
}
