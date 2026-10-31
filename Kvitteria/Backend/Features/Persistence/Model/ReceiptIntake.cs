using Backend.Features.Persistence.Model.Users;
using Kvitteria.Contracts.Shared;
namespace Backend.Features.Persistence.Model;

/// <summary>
/// One fetched mail from the moment it is recorded until it becomes a receipt. Written before the mail
/// is flagged <c>Seen</c> and deleted in the transaction that inserts the receipt, so a row means
/// "unresolved mail" and the mail text lives here only as long as it is needed. A failed mail keeps its
/// row until someone retries or dismisses it.
/// </summary>
public sealed class ReceiptIntake
{
	public Guid Id { get; set; }

	/// <summary>IMAP Message-Id. Null when the mail had none, in which case it cannot be deduplicated.</summary>
	public string? SourceMessageId { get; set; }

	public string From { get; set; } = "";
	public string Subject { get; set; } = "";
	public DateTime MailDate { get; set; }
	public string BodyText { get; set; } = "";

	/// <summary>"text" | "html" | "none".</summary>
	public string BodySource { get; set; } = "";

	/// <summary>
	/// The mailbox it arrived through. SetNull on delete: a failed mail outlives its mailbox and can
	/// still be retried, keeping the member.
	/// </summary>
	public Guid? MailboxId { get; set; }
	public Mailbox? Mailbox { get; set; }

	/// <summary>The member it belongs to. Restrict on delete, like everything that records history.</summary>
	public Guid AppUserId { get; set; }
	public AppUser? AppUser { get; set; }

	/// <summary>Only <c>Queued</c>, <c>Extracting</c>, <c>Extracted</c> and <c>Failed</c> are ever stored.</summary>
	public ReceiptStatus Status { get; set; } = ReceiptStatus.Queued;

	public string? Error { get; set; }
	public int Attempts { get; set; }
	public DateTime ReceivedAt { get; set; }
	public DateTime? FailedAt { get; set; }
}
