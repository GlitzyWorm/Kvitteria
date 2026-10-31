using Backend.Features.Extraction;
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
	Guid appUserId
)
{
	/// <summary>The durable <c>ReceiptIntake</c> row this record mirrors; every state change is written there.</summary>
	public Guid IntakeId { get; } = intakeId;

	/// <summary>The mailbox it arrived through, as of when it was recorded.</summary>
	public Guid? MailboxId { get; } = mailboxId;

	public string? MessageId { get; } = messageId;
	public string From { get; } = from;
	public string Subject { get; } = subject;
	public DateTimeOffset Date { get; } = date;
	public string BodyText { get; } = bodyText;
	public string BodySource { get; } = bodySource; // "text" | "html" | "none"

	/// <summary>The household member whose mailbox this arrived in.</summary>
	public Guid AppUserId { get; } = appUserId;

	public ReceiptStatus Status { get; set; } = ReceiptStatus.Queued;
	public ExtractedReceipt? Extracted { get; set; }
	public int Attempts { get; set; }
	public bool Reconciled { get; set; }
	public string? Error { get; set; }
	public DateTimeOffset EnqueuedAt { get; init; }
	public DateTimeOffset? CompletedAt { get; set; }
}