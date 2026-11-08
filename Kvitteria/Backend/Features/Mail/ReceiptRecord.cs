using Backend.Features.Extraction;
using Backend.Features.Persistence.Model;
using Kvitteria.Contracts.Shared;

namespace Backend.Features.Mail;

public sealed class ReceiptRecord(
	Guid intakeId,
	Guid? mailboxId,
	string? messageId,
	string from,
	string subject,
	DateTimeOffset date,
	string bodyText,
	string bodySource,
	Guid appUserId,
	IntakeSource source = IntakeSource.Mail,
	Guid? imageId = null
)
{
	/// <summary>The durable <c>ReceiptIntake</c> row this record mirrors; every state change is written there.</summary>
	public Guid IntakeId { get; } = intakeId;

	/// <summary>The mailbox it arrived through, as of when it was recorded. Null for an upload.</summary>
	public Guid? MailboxId { get; } = mailboxId;

	public string? MessageId { get; } = messageId;
	public string From { get; } = from;
	public string Subject { get; } = subject;

	/// <summary>The mail's date, or an upload's capture time and otherwise its upload time.</summary>
	public DateTimeOffset Date { get; } = date;

	/// <summary>
	/// The mail text, or an upload's transcription. Empty on an upload until the extraction worker
	/// transcribes it, which then sets it here as well as on the row.
	/// </summary>
	public string BodyText { get; set; } = bodyText;

	public string BodySource { get; } = bodySource; // "text" | "html" | "none" | "image"

	/// <summary>The household member whose mailbox this arrived in, or who uploaded it.</summary>
	public Guid AppUserId { get; } = appUserId;

	public IntakeSource Source { get; } = source;

	/// <summary>An upload's image. Null for mail.</summary>
	public Guid? ImageId { get; } = imageId;

	/// <summary>How logs name it: the subject for mail, which an upload does not have.</summary>
	public string Label => Source == IntakeSource.Upload ? $"upload {IntakeId}" : Subject;

	public ReceiptStatus Status { get; set; } = ReceiptStatus.Queued;
	public ExtractedReceipt? Extracted { get; set; }
	public int Attempts { get; set; }
	public bool Reconciled { get; set; }
	public string? Error { get; set; }
	public DateTimeOffset EnqueuedAt { get; init; }
	public DateTimeOffset? CompletedAt { get; set; }
}
