// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Shared;

/// <summary>
/// What the mailbox sheet edits, before it becomes a <c>CreateMailboxDto</c> or a
/// <c>PutMailboxDto</c>.
/// </summary>
/// <remarks>
/// Deliberately not one of those DTOs. This type is validated by <see cref="MailboxDraftValidator"/>,
/// which is linked into MobileApp.Tests as source — and that project holds no project reference at
/// all, not even to Contracts. Mapping to the wire types happens in <c>MailboxApiClient</c>, at the
/// boundary where a Contracts reference is free.
///
/// <c>Port</c> is a string because the field it is bound to is: a half-typed port is not an int, and
/// an <c>Entry</c> bound to one would fight the person typing it.
/// </remarks>
public sealed record MailboxDraft
{
	/// <summary>Absent while creating; the mailbox being edited otherwise.</summary>
	public Guid? Id { get; init; }

	public Guid OwnerId { get; init; }

	/// <summary>
	/// Whether the owner may still be chosen. False only when adding on someone else's behalf: an
	/// existing mailbox cannot change hands, and "add my mailbox" means mine.
	/// </summary>
	public bool OwnerLocked { get; init; }

	public string Host { get; init; } = string.Empty;

	public string Port { get; init; } = "993";

	public bool UseSsl { get; init; } = true;

	public string Folder { get; init; } = string.Empty;

	public string Username { get; init; } = string.Empty;

	/// <summary>
	/// Always starts empty, including when editing — the server never returns a password, so there is
	/// nothing to prefill. Left empty on an edit it means "keep the stored one".
	/// </summary>
	public string Password { get; init; } = string.Empty;

	public bool IsEnabled { get; init; } = true;

	public bool IsEdit => Id.HasValue;
}

/// <summary>
/// The same completeness checking the server's <c>MailboxValidation.TryValidate</c> does, run before
/// the request rather than after it — an empty field should not cost a round trip and an IMAP
/// connection attempt.
/// </summary>
/// <remarks>
/// It deliberately does <b>not</b> check for a duplicate folder. The server owns that answer through a
/// unique index and reports it as a 409 naming the host and username; a second opinion here could
/// only ever disagree with it.
/// </remarks>
public static class MailboxDraftValidator
{
	public const int MinPort = 1;
	public const int MaxPort = 65535;

	/// <summary>
	/// True when the draft is worth sending. Otherwise <paramref name="errorKey"/> is a resource key,
	/// not a message — the same arrangement <c>AuthErrorMapper</c> uses, which keeps localization in
	/// the view model and this method free of a language.
	/// </summary>
	public static bool TryValidate(MailboxDraft draft, out string? errorKey)
	{
		if (string.IsNullOrWhiteSpace(draft.Host) ||
		    string.IsNullOrWhiteSpace(draft.Folder) ||
		    string.IsNullOrWhiteSpace(draft.Username))
		{
			errorKey = "MailboxErrorRequiredFields";
			return false;
		}

		// Invariant parsing: the field accepts digits, and a Danish device must not read "993" through
		// a culture that treats a separator differently.
		if (!int.TryParse(draft.Port, System.Globalization.NumberStyles.None,
			    System.Globalization.CultureInfo.InvariantCulture, out int port) ||
		    port < MinPort || port > MaxPort)
		{
			errorKey = "MailboxErrorPort";
			return false;
		}

		// A new mailbox has no stored password to fall back on, so this is the one case where an empty
		// one is a mistake rather than an instruction.
		if (!draft.IsEdit && string.IsNullOrWhiteSpace(draft.Password))
		{
			errorKey = "MailboxErrorPasswordRequired";
			return false;
		}

		errorKey = null;
		return true;
	}

	/// <summary>The port as the wire wants it. Only meaningful once <see cref="TryValidate"/> passed.</summary>
	public static int ParsePort(MailboxDraft draft) =>
		int.Parse(draft.Port, System.Globalization.CultureInfo.InvariantCulture);
}
