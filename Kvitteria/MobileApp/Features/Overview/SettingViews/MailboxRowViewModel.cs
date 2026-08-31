// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Windows.Input;
using Kvitteria.Contracts.DTO;
using MobileApp.Features.Shared;

namespace MobileApp.Features.Overview.SettingViews;

/// <summary>
/// One mailbox as a row renders it. Flat and read-only: the cards bind to this, and every change goes
/// through the sheet and a reload rather than by mutating a row in place — so what is on screen is
/// always something the server confirmed.
/// </summary>
public sealed class MailboxRowViewModel
{
	public MailboxRowViewModel(MailboxDto mailbox, string unknownMemberName, bool canManage, ICommand editCommand)
	{
		CanManage = canManage;
		Id = mailbox.Id;
		Folder = mailbox.Folder;
		Host = $"{mailbox.Host}:{mailbox.Port}";
		Username = mailbox.Username;
		IsEnabled = mailbox.IsEnabled;
		LastError = mailbox.LastError;

		// MemberDisplayName is "" when the owning AppUser could not be loaded. Falling back to the
		// caller's localized "unknown member" keeps an orphaned mailbox visible and editable rather
		// than rendering a blank row nobody can account for.
		MemberName = string.IsNullOrWhiteSpace(mailbox.MemberDisplayName)
			? unknownMemberName
			: mailbox.MemberDisplayName;

		Initials = Shared.Initials.From(mailbox.MemberDisplayName);
		EditCommand = editCommand;
		Mailbox = mailbox;
	}

	public Guid Id { get; }

	public string Folder { get; }

	/// <summary>Host and port together, which is what identifies a server to whoever typed it.</summary>
	public string Host { get; }

	public string Username { get; }

	public string MemberName { get; }

	public string Initials { get; }

	public bool IsEnabled { get; }

	/// <summary>
	/// The last polling failure, if the mailbox is currently broken. The one signal that receipts have
	/// silently stopped arriving, which is why it is on the card at all — neither the source nor the
	/// screenshots show it.
	/// </summary>
	public string? LastError { get; }

	public bool HasError => !string.IsNullOrWhiteSpace(LastError);

	/// <summary>
	/// Whether the caller holds ManageMailboxes, and so whether this row offers a manage control. On the
	/// row rather than reached for on the tab: a template binding out of a BindableLayout and into its
	/// host is both fragile and unreadable.
	/// </summary>
	public bool CanManage { get; }

	public ICommand EditCommand { get; }

	/// <summary>The row it was built from, so editing can seed a draft without re-fetching.</summary>
	public MailboxDto Mailbox { get; }
}
