// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Auth;

public partial class GatePage : ContentPage
{
	private readonly GateViewModel _viewModel;

	public GatePage(GateViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>
	/// Resolves on every appearance, not once in the constructor. Signing out navigates back here,
	/// and Shell keeps the page instance, so a one-shot in the constructor would leave the second
	/// visit on a splash that never finishes.
	/// </summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.ResolveAsync();
	}
}
