using Backend.Features.Persistence.Model.Users;
using Kvitteria.Contracts.Shared;
namespace Backend.Features.Persistence.Model;

/// <summary>
/// One fetched mail or uploaded image from the moment it is recorded until it becomes a receipt. A mail
/// is written before it is flagged <c>Seen</c>, an upload before the 202, and either is deleted in the
/// transaction that inserts the receipt, so a row means "unresolved intake" and the mail text or
/// transcription lives here only as long as it is needed. A failed intake keeps its row until someone
/// retries or dismisses it.
/// </summary>
public sealed class ReceiptIntake
{
	public Guid Id { get; set; }

	/// <summary>Mail or upload. Existing rows migrated to <see cref="IntakeSource.Mail"/>.</summary>
	public IntakeSource Source { get; set; } = IntakeSource.Mail;

	/// <summary>IMAP Message-Id. Null when the mail had none, in which case it cannot be deduplicated.</summary>
	public string? SourceMessageId { get; set; }

	/// <summary>Empty for an upload.</summary>
	public string From { get; set; } = "";

	/// <summary>Empty for an upload.</summary>
	public string Subject { get; set; } = "";

	/// <summary>
	/// For an upload, <see cref="CapturedAt"/> or else <see cref="ReceivedAt"/>, which is what makes
	/// <c>PurchaseDate.Resolve</c>'s fallback the capture time without a path of its own.
	/// </summary>
	public DateTime MailDate { get; set; }

	/// <summary>
	/// The mail text, or an upload's transcription. Empty on an upload until it is transcribed, which is
	/// what "has a transcription" means.
	/// </summary>
	public string BodyText { get; set; } = "";

	/// <summary>"text" | "html" | "none" | "image".</summary>
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

	/// <summary>When the image was taken, as the phone reported it. Null for mail and when unknown.</summary>
	public DateTime? CapturedAt { get; set; }

	/// <summary>An upload's image. Restrict: dismiss and ingestion move or delete it explicitly.</summary>
	public Guid? ImageId { get; set; }
	public ReceiptImage? Image { get; set; }
}
