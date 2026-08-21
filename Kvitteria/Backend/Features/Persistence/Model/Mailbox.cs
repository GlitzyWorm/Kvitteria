using Backend.Features.Persistence.Model.Users;
namespace Backend.Features.Persistence.Model;

/// <summary>
/// One household member's receipt folder on an IMAP server. Replaces the <c>Imap:Folders</c> config
/// list so mailboxes can be managed from a frontend, and so a folder is owned by a real
/// <see cref="AppUser"/> instead of the free-text member name and email the config carried.
/// </summary>
public sealed class Mailbox
{
	public Guid Id { get; set; }

	/// <summary>
	/// The member whose mail this is. Restrict on delete: members are disabled, never removed, so a
	/// mailbox can never be orphaned.
	/// </summary>
	public Guid AppUserId { get; set; }
	public AppUser? AppUser { get; set; }

	public string Host { get; set; } = "";
	public int Port { get; set; } = 993;
	public bool UseSsl { get; set; } = true;
	public string Username { get; set; } = "";

	/// <summary>
	/// Data-Protection ciphertext. The plaintext is only ever materialized at the two moments that need
	/// it — writing the row, and authenticating against the server — so this is deliberately not an EF
	/// value converter, which would decrypt on every read including list queries.
	/// </summary>
	public string EncryptedPassword { get; set; } = "";

	/// <summary>The folder receipts land in, e.g. <c>INBOX.Netto</c>.</summary>
	public string Folder { get; set; } = "";

	/// <summary>Skipped by the fetcher while false, but keeps its credentials — pausing, not deleting.</summary>
	public bool IsEnabled { get; set; } = true;

	/// <summary>Last poll that reached the server and completed. Written by the fetcher, read-only over HTTP.</summary>
	public DateTime? LastPolledAt { get; set; }

	/// <summary>Why the last poll of this mailbox failed, or null when the last one succeeded.</summary>
	public string? LastError { get; set; }

	/// <summary>
	/// Failures since the last success. A frontend can use it to distinguish a blip from a mailbox that
	/// has been broken for hours.
	/// </summary>
	public int ConsecutiveFailures { get; set; }

	public DateTime CreatedAt { get; set; }
}
