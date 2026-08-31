// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Maui.Extensions;
using MobileApp.Features.Shared;

namespace MobileApp.Features.Overview;

public partial class OverviewPage : ContentPage
{
	private readonly OverviewViewModel _viewModel;
	private readonly INavigator _navigator;

	public OverviewPage(OverviewViewModel viewModel, INavigator navigator)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
        _navigator = navigator;
	}

	/// <summary>
	/// Both lines are composed in code rather than bound through the localize extension, so they are
	/// rebuilt per appearance — which is also when the resolved member and the hour of day may have
	/// changed.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.Refresh();
	}

	/// <summary>
	/// A factory rather than an injected instance: the sheet is transient, and reusing one across
	/// shows would try to add a view that already has a parent.
	/// </summary>
	private async void OnAccountClicked(object? sender, EventArgs e)
    {
        await _navigator.GoToAsync(AppRoutes.Settings);
    }
}
