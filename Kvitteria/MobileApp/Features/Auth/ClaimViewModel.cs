// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.Input;
using MobileApp.Features.Shared;
using MobileApp.Localization;

namespace MobileApp.Features.Auth;

/// <summary>
/// The screen a server with no members shows before anyone signs up.
/// </summary>
/// <remarks>
/// It calls nothing. There is no claim endpoint to call: the household is claimed as a side effect
/// of the first authenticated request, inside <c>UserProvisioning.Decide</c>, under an advisory lock
/// so two people racing cannot both become admin. The whole purpose of this screen is that the
/// person about to sign up learns beforehand what signing up will make them — the mockup carries
/// the same sentence as a footnote on its gate, which is easy to sign past without reading.
/// </remarks>
public sealed partial class ClaimViewModel(INavigator navigator, ILocalizationService localization)
	: ViewModelBase(localization)
{
	/// <summary>
	/// Straight to the sign-up half of the gate. Sign-in is deliberately still reachable there: an
	/// account can exist at GoTrue on a database whose household rows were wiped, and that person
	/// should be able to claim by signing in rather than being told to create a second account.
	/// </summary>
	[RelayCommand]
	private Task ContinueAsync() => navigator.GoToAsync(AppRoutes.AuthSignUp);
}
