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
/// How the sheet ended, from the point of view of whatever opened it. Declared here rather than beside
/// IDialogService so the sheet's XAML can name it through the same vm: alias as its data type — a
/// generic Popup needs its type argument spelled in markup.
/// </summary>
public enum MailboxSheetResult
{
	/// <summary>Dismissed without writing anything — cancelled, or tapped outside.</summary>
	Cancelled,

	/// <summary>Created or saved. The list behind it is now stale.</summary>
	Saved,

	/// <summary>Removed. Likewise.</summary>
	Removed
}

/// <summary>One member as the owner picker lists them.</summary>
public sealed record MemberOption(Guid Id, string DisplayName)
{
	/// <summary>What the Picker shows. ToString rather than ItemDisplayBinding: one place, not two.</summary>
	public override string ToString() => DisplayName;
}

/// <summary>
/// The add/edit mailbox sheet. Owns the draft, validates it before spending a round trip, and performs
/// its own write — the tab behind it only learns whether the list is now stale.
/// </summary>
/// <remarks>
/// It does not close itself; <c>MailboxSheet</c> does, because closing is a view concern
/// (<c>Popup&lt;T&gt;.CloseAsync</c>) and a view model that held a popup reference would be back to
/// reaching into the visual tree. The view hands over a callback in <see cref="Prepare"/>.
/// </remarks>
public sealed partial class MailboxSheetViewModel(
	MailboxApiClient mailboxes,
	INavigator navigator,
	ILocalizationService localization) : ViewModelBase(localization)
{
	private Func<MailboxSheetResult, Task>? _close;

	[ObservableProperty]
	public partial string Title { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string Host { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string Port { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool UseSsl { get; set; }

	[ObservableProperty]
	public partial string Folder { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string Username { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string Password { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool IsEnabled { get; set; }

	/// <summary>Whatever went wrong, in whatever language it arrived in. Empty erases the callout.</summary>
	[ObservableProperty]
	public partial string? Error { get; set; }

	[ObservableProperty]
	public partial MemberOption? Owner { get; set; }

	/// <summary>Editing an existing mailbox, rather than adding one.</summary>
	[ObservableProperty]
	public partial bool IsEdit { get; set; }

	/// <summary>
	/// False when the owner may still be chosen. Bound to the Picker's IsEnabled inverted, and true for
	/// every edit and for "add my mailbox".
	/// </summary>
	[ObservableProperty]
	public partial bool OwnerLocked { get; set; }

	public ObservableCollection<MemberOption> Members { get; } = [];

	private Guid? _id;

	/// <summary>
	/// Loads a draft into the fields. Called by the view rather than the constructor because the sheet
	/// is resolved once per showing but its content is different each time.
	/// </summary>
	public void Prepare(MailboxDraft draft, IReadOnlyList<AppUserDto> members, Func<MailboxSheetResult, Task> close)
	{
		_id = draft.Id;
		_close = close;

		IsEdit = draft.IsEdit;
		OwnerLocked = draft.OwnerLocked;
		Title = Localization[draft.IsEdit ? "MailboxEdit" : "MailboxNew"];

		Members.Clear();

		// Disabled members are excluded, matching the server: it refuses an owner who is neither active
		// nor pending, and offering one here would only produce a 400 nobody could act on.
		foreach (AppUserDto member in members.Where(m => m.Status != nameof(MemberStatus.Disabled)))
		{
			Members.Add(new MemberOption(member.Id, member.DisplayName));
		}

		Owner = Members.FirstOrDefault(m => m.Id == draft.OwnerId) ?? Members.FirstOrDefault();

		Host = draft.Host;
		Port = draft.Port;
		UseSsl = draft.UseSsl;
		Folder = draft.Folder;
		Username = draft.Username;
		Password = draft.Password;
		IsEnabled = draft.IsEnabled;
		Error = null;
	}

	[RelayCommand]
	private async Task SaveAsync()
	{
		if (IsBusy)
		{
			return;
		}

		if (Owner is null)
		{
			Error = Localization["MailboxErrorNoOwner"];
			return;
		}

		MailboxDraft draft = Current(Owner.Id);

		// Client-side first, so an empty field costs neither a round trip nor an IMAP connection
		// attempt. The server checks the same things again and owns the duplicate question.
		if (!MailboxDraftValidator.TryValidate(draft, out string? errorKey))
		{
			Error = Localize(errorKey);
			return;
		}

		IsBusy = true;
		Error = null;

		try
		{
			ApiResult<MailboxDto> result = _id is { } id
				? await mailboxes.UpdateAsync(id, draft, CancellationToken.None)
				: await mailboxes.CreateAsync(draft, CancellationToken.None);

			if (result.IsOk)
			{
				await CloseAsync(MailboxSheetResult.Saved);
				return;
			}

			Error = Describe(result.Outcome, result.Detail);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task RemoveAsync()
	{
		if (IsBusy || _id is not { } id)
		{
			return;
		}

		// A mailbox carries an encrypted password nobody can recover, so this follows the same
		// confirm-first rule as signing out and changing server.
		bool confirmed = await navigator.ConfirmAsync(
			Localization["ConfirmRemoveMailboxTitle"],
			Localization["ConfirmRemoveMailboxMessage"],
			Localization["Remove"],
			Localization["Cancel"]);

		if (!confirmed)
		{
			return;
		}

		IsBusy = true;
		Error = null;

		try
		{
			ApiResult<bool> result = await mailboxes.DeleteAsync(id, CancellationToken.None);

			// Already gone is the outcome the caller wanted, so it closes rather than complaining.
			if (result.IsOk || result.Outcome is ApiOutcome.NotFound)
			{
				await CloseAsync(MailboxSheetResult.Removed);
				return;
			}

			Error = Describe(result.Outcome, result.Detail);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private Task CancelAsync() => CloseAsync(MailboxSheetResult.Cancelled);

	private MailboxDraft Current(Guid ownerId) => new()
	{
		Id = _id,
		OwnerId = ownerId,
		OwnerLocked = OwnerLocked,
		Host = Host,
		Port = Port,
		UseSsl = UseSsl,
		Folder = Folder,
		Username = Username,
		Password = Password,
		IsEnabled = IsEnabled
	};

	private Task CloseAsync(MailboxSheetResult result) =>
		_close?.Invoke(result) ?? Task.CompletedTask;

	/// <summary>
	/// What to show for a failure. The server's own sentence is used verbatim where it knows something
	/// this app cannot work out — a rejected credential, a folder that does not exist (the message
	/// carries the list of folders that do), a duplicate naming the host and username. Those arrive in
	/// English, which is the trade: a localized "could not be verified" would throw away the only
	/// diagnostic worth having. Everything the app can categorize itself is localized.
	/// </summary>
	private string Describe(ApiOutcome outcome, string? detail) => outcome switch
	{
		ApiOutcome.Verification or ApiOutcome.Conflict or ApiOutcome.Invalid when !string.IsNullOrWhiteSpace(detail) =>
			detail,
		ApiOutcome.Forbidden => Localization["MailboxErrorForbidden"],
		ApiOutcome.NotFound => Localization["MailboxErrorNotFound"],
		ApiOutcome.Unauthenticated => Localization["MailboxErrorUnauthenticated"],
		_ => Localization["MailboxErrorUnreachable"]
	};

	/// <summary>The status names the backend spells, as <c>AppUserDto.Status</c> carries them.</summary>
	private enum MemberStatus
	{
		Pending,
		Active,
		Disabled
	}
}
