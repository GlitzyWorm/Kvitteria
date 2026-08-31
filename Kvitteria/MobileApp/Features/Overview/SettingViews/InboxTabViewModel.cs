// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kvitteria.Contracts.DTO;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using MobileApp.Services;

namespace MobileApp.Features.Overview.SettingViews;

/// <summary>
/// The household's IMAP mailboxes: mine, everyone else's, and the sheet that edits them.
/// </summary>
/// <remarks>
/// Reads the resolved member from <see cref="ISessionService.User"/> rather than asking the server
/// again — the gate has already fetched and cached the <c>CurrentUserDto</c>, and both answers this
/// screen needs from it (my own id, and whether I hold ManageMailboxes) are already on it.
/// </remarks>
public sealed partial class InboxTabViewModel(
	MailboxApiClient mailboxes,
	ISessionService sessions,
	IDialogService dialogs,
	ILocalizationService localization) : ViewModelBase(localization), IRefreshable
{
	private const string ManageMailboxes = "ManageMailboxes";

	/// <summary>Every mailbox I own — all of them, so a second one is never left unreachable.</summary>
	public ObservableCollection<MailboxRowViewModel> MyMailboxes { get; } = [];

	public ObservableCollection<MailboxRowViewModel> HouseholdMailboxes { get; } = [];

	/// <summary>The members a mailbox may be assigned to, handed to the sheet when it opens.</summary>
	private readonly List<AppUserDto> _members = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsReadOnly))]
	[NotifyPropertyChangedFor(nameof(CanAddAnotherOfMine))]
	public partial bool CanManage { get; set; }

	/// <summary>Drives the footnote explaining why the add and manage controls are absent.</summary>
	public bool IsReadOnly => !CanManage;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasMyMailbox))]
	[NotifyPropertyChangedFor(nameof(HasNoMyMailbox))]
	[NotifyPropertyChangedFor(nameof(CanAddAnotherOfMine))]
	public partial int MyMailboxCount { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoHouseholdMailboxes))]
	public partial int HouseholdMailboxCount { get; set; }

	// A count plus derived flags rather than binding to Collection.Count: an ObservableCollection
	// raises CollectionChanged, which a plain Count binding does not listen to.
	public bool HasMyMailbox => MyMailboxCount > 0;

	public bool HasNoMyMailbox => MyMailboxCount == 0;

	public bool HasNoHouseholdMailboxes => HouseholdMailboxCount == 0;

	/// <summary>
	/// Whether to offer a second mailbox of my own. Only once I have one — before that the empty state's
	/// own button is the call to action, and two add buttons would be showing at once. A member may keep
	/// more than one folder: the server's uniqueness is per folder, not per member.
	/// </summary>
	public bool CanAddAnotherOfMine => CanManage && HasMyMailbox;

	/// <summary>A whole-tab failure — the list could not be loaded at all.</summary>
	[ObservableProperty]
	public partial string? Error { get; set; }

	/// <inheritdoc />
	public async Task RefreshAsync(CancellationToken ct)
	{
		if (IsBusy)
		{
			return;
		}

		IsBusy = true;
		Error = null;

		try
		{
			CurrentUserDto? me = sessions.User;
			CanManage = me?.Permissions.Contains(ManageMailboxes) is true;

			ApiResult<List<MailboxDto>> result = await mailboxes.GetMailboxesAsync(ct);

			if (!result.IsOk || result.Value is null)
			{
				Error = Localization[result.Outcome is ApiOutcome.Unauthenticated
					? "MailboxErrorUnauthenticated"
					: "MailboxErrorUnreachable"];

				Fill([], me?.Id);
				return;
			}

			Fill(result.Value, me?.Id);
			await LoadMembersAsync(ct);
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// The owner options for the sheet. A failure here is deliberately not surfaced: the list itself
	/// loaded, and someone editing their own mailbox never needs to choose an owner. The sheet falls
	/// back to whoever already owns the row.
	/// </summary>
	private async Task LoadMembersAsync(CancellationToken ct)
	{
		ApiResult<List<AppUserDto>> members = await mailboxes.GetMembersAsync(ct);

		_members.Clear();

		if (members.IsOk && members.Value is not null)
		{
			_members.AddRange(members.Value);
		}
	}

	/// <summary>
	/// Splits the household's mailboxes into mine and everyone else's. Mine means every row whose owner
	/// is me — the source shows only the first, which would leave a second one in neither list and so
	/// impossible to edit or remove.
	/// </summary>
	private void Fill(List<MailboxDto> all, Guid? myId)
	{
		MyMailboxes.Clear();
		HouseholdMailboxes.Clear();

		string unknown = Localization["UnknownMember"];

		foreach (MailboxDto mailbox in all)
		{
			var row = new MailboxRowViewModel(mailbox, unknown, CanManage, EditCommand);

			if (myId.HasValue && mailbox.AppUserId == myId.Value)
			{
				MyMailboxes.Add(row);
			}
			else
			{
				HouseholdMailboxes.Add(row);
			}
		}

		MyMailboxCount = MyMailboxes.Count;
		HouseholdMailboxCount = HouseholdMailboxes.Count;
	}

	/// <summary>
	/// Reloading from the retry button. A command wrapper around <see cref="RefreshAsync"/>, which is the
	/// interface method and so generates none of its own.
	/// </summary>
	[RelayCommand]
	private Task ReloadAsync() => RefreshAsync(CancellationToken.None);

	/// <summary>Adds a mailbox for me. The owner is fixed, which is what "min indbakke" means.</summary>
	[RelayCommand]
	private Task AddMineAsync()
	{
		Guid? myId = sessions.User?.Id;

		return myId is null
			? Task.CompletedTask
			: OpenAsync(new MailboxDraft { OwnerId = myId.Value, OwnerLocked = true });
	}

	/// <summary>Adds a mailbox on another member's behalf, so the owner is still to be chosen.</summary>
	[RelayCommand]
	private Task AddOtherAsync() =>
		OpenAsync(new MailboxDraft { OwnerId = sessions.User?.Id ?? Guid.Empty, OwnerLocked = false });

	[RelayCommand]
	private Task EditAsync(MailboxRowViewModel? row)
	{
		if (row is null)
		{
			return Task.CompletedTask;
		}

		MailboxDto mailbox = row.Mailbox;

		return OpenAsync(new MailboxDraft
		{
			Id = mailbox.Id,
			OwnerId = mailbox.AppUserId,

			// An existing mailbox does not change hands: its stored password belongs to that account.
			OwnerLocked = true,
			Host = mailbox.Host,
			Port = mailbox.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
			UseSsl = mailbox.UseSsl,
			Folder = mailbox.Folder,
			Username = mailbox.Username,

			// Always blank. The server never returns the password, so there is nothing to prefill, and
			// left blank on save it means "keep the stored one".
			Password = string.Empty,
			IsEnabled = mailbox.IsEnabled
		});
	}

	private async Task OpenAsync(MailboxDraft draft)
	{
		if (!CanManage)
		{
			return;
		}

		MailboxSheetResult result = await dialogs.ShowMailboxSheetAsync(draft, _members);

		// Reload rather than patch the collection in place, so what is on screen is only ever something
		// the server confirmed — including the fields it trimmed on the way in.
		if (result is not MailboxSheetResult.Cancelled)
		{
			await RefreshAsync(CancellationToken.None);
		}
	}
}
