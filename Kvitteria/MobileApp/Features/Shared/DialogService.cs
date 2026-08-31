// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using Kvitteria.Contracts.DTO;
using MobileApp.Features.Overview.SettingViews;

namespace MobileApp.Features.Shared;

/// <summary>
/// Showing something over the current page.
/// </summary>
/// <remarks>
/// Deliberately not part of <see cref="INavigator"/>, which stays a two-method abstraction over routes
/// and confirmations. The reason either exists is the same: a view model must not reach into
/// <c>Shell.Current</c>, and <c>ShowPopupAsync</c> hangs off <c>Page</c>/<c>Shell</c> rather than off
/// anything a view model has.
/// </remarks>
public interface IDialogService
{
	/// <summary>
	/// Opens the mailbox sheet over the current page and answers what the person did with it. The sheet
	/// performs its own writes, so the caller only has to know whether the list behind it changed.
	/// </summary>
	/// <param name="members">
	/// Who the mailbox may belong to. Passed in rather than fetched here, so opening the sheet three
	/// times does not ask the server the same question three times — the tab already holds this list.
	/// </param>
	Task<MailboxSheetResult> ShowMailboxSheetAsync(MailboxDraft draft, IReadOnlyList<AppUserDto> members);
}

/// <inheritdoc />
/// <remarks>
/// The popup itself is resolved from the container rather than newed up, so its view model arrives with
/// its dependencies the way every page's does.
/// </remarks>
public sealed class ShellDialogService(IServiceProvider services) : IDialogService
{
	public async Task<MailboxSheetResult> ShowMailboxSheetAsync(MailboxDraft draft, IReadOnlyList<AppUserDto> members)
	{
		Shell? shell = Shell.Current;

		if (shell is null)
		{
			throw new InvalidOperationException("Cannot show a dialog: no Shell is current.");
		}

		var sheet = services.GetRequiredService<Overview.SettingViews.MailboxSheet>();
		sheet.Prepare(draft, members);

		IPopupResult<MailboxSheetResult> result =
			await shell.ShowPopupAsync<MailboxSheetResult>(sheet, Options());

		// Tapping the dimmed backdrop closes without a result. Treated as a cancel, not as a save:
		// nothing was written, and reloading on it would only be harmless rather than correct.
		return result.WasDismissedByTappingOutsideOfPopup
			? MailboxSheetResult.Cancelled
			: result.Result;
	}

	/// <summary>
	/// The scrim and dismiss behaviour, from App.xaml. Resolved through the resource dictionary rather
	/// than constructed here so the backdrop colour stays a token — see the Scrim entry in Colors.xaml.
	/// </summary>
	private static IPopupOptions Options() =>
		Application.Current?.Resources.TryGetValue("SheetPopupOptions", out object? options) is true &&
		options is IPopupOptions popupOptions
			? popupOptions
			: PopupOptions.Empty;
}
