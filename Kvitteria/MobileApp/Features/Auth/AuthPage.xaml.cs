// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Auth;

/// <summary>
/// The gate. Two query parameters steer which state it opens in — <c>mode=up</c> from the claim
/// screen, <c>state=rejected</c> from the gate after a 403.
/// </summary>
[QueryProperty(nameof(Mode), "mode")]
[QueryProperty(nameof(State), "state")]
public partial class AuthPage : ContentPage
{
	private readonly AuthViewModel _viewModel;

	public AuthPage(AuthViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public string? Mode { get; set; }

	public string? State { get; set; }

	/// <summary>
	/// Applied on appearance rather than in the query property setters. Shell assigns those one at a
	/// time and in no guaranteed order, so acting on the first would decide the screen's state from
	/// half the route.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.Prepare(Mode, State);

		// Consumed, so navigating back here plainly does not resurrect the previous route's state.
		Mode = null;
		State = null;
	}
}
