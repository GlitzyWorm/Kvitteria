// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Maui.Views;
using Kvitteria.Contracts.DTO;
using MobileApp.Features.Shared;

namespace MobileApp.Features.Overview.SettingViews;

/// <summary>
/// The add/edit mailbox dialog, shown over the settings page.
/// </summary>
/// <remarks>
/// A <c>Popup&lt;T&gt;</c> rather than a page, because the source presents this as a card over a dimmed
/// page rather than as a screen of its own. Closing is the one thing the view has to own:
/// <c>CloseAsync</c> lives on the popup, so the view model is handed it as a callback instead of being
/// given a reference to the visual tree.
///
/// Note this is the modern shape — in toolkit versions before 12 the only way to close was
/// <c>ClosePopupAsync</c> hanging off the <b>page</b> the popup was shown over, which is what the
/// gotcha in CLAUDE.md is about.
/// </remarks>
public partial class MailboxSheet : Popup<MailboxSheetResult>
{
	private readonly MailboxSheetViewModel _viewModel;

	public MailboxSheet(MailboxSheetViewModel viewModel)
	{
		_viewModel = viewModel;
		BindingContext = viewModel;

		InitializeComponent();
	}

	/// <summary>
	/// Loads the draft to edit. Separate from the constructor because what the sheet shows differs each
	/// time it is opened, while the instance itself is resolved from the container.
	/// </summary>
	public void Prepare(MailboxDraft draft, IReadOnlyList<AppUserDto> members) =>
		_viewModel.Prepare(draft, members, CloseAsync);

	/// <summary>
	/// Dismisses the popup with a result. Handed to the view model in <see cref="Prepare"/> so it can
	/// close itself after a successful write without knowing what a popup is.
	/// </summary>
	private Task CloseAsync(MailboxSheetResult result) => CloseAsync(result, CancellationToken.None);
}
