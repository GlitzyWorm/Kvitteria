// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Shared;
using MobileApp.Services;

namespace MobileApp;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services, ISessionService sessions, INavigator navigator)
	{
		InitializeComponent();

		_services = services;

		// The Kvitteria palette is dark-only — there is no light ramp designed, so the styles carry
		// flat token colours rather than AppThemeBinding pairs. Without pinning the theme, a phone in
		// light mode would still hand platform chrome (pickers, dialogs, the keyboard, the Android
		// status bar) its light colours over this dark ground.
		UserAppTheme = AppTheme.Dark;

		// A refresh that the server refused ends the session wherever the user happens to be, so the
		// return to the gate is wired here rather than on any one page.
		sessions.SessionExpired += async (_, _) =>
			await MainThread.InvokeOnMainThreadAsync(() => navigator.GoToAsync(AppRoutes.Gate));
	}

	/// <summary>
	/// The Shell is resolved rather than constructed: it needs the localization service to title its
	/// tabs, and its pages are resolved from the same container.
	/// </summary>
	protected override Window CreateWindow(IActivationState? activationState)
		=> new(_services.GetRequiredService<AppShell>());
}
