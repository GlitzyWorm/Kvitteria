// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Shared;

/// <summary>
/// Moving between the routes in <see cref="AppRoutes"/>. One interface rather than
/// <c>Shell.Current</c> scattered through the view models, so navigation is injected like every
/// other dependency and a view model does not reach into a static.
/// </summary>
public interface INavigator
{
	Task GoToAsync(string route);

	/// <summary>A yes/no the user must answer before something irreversible happens.</summary>
	Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}

/// <inheritdoc />
public sealed class ShellNavigator : INavigator
{
	public Task GoToAsync(string route)
	{
		Shell? shell = Shell.Current;

		// Null while the Shell is still being constructed — the gate resolves on appearing, so this
		// should not happen, and swallowing it silently would present as a frozen splash.
		return shell is null
			? throw new InvalidOperationException($"Cannot navigate to {route}: no Shell is current.")
			: shell.GoToAsync(route);
	}

	public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
		=> Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
}
