using Backend.Features.Extraction;
using Backend.Features.Shared;
namespace Backend.Features.Mail;

public sealed class ReceiptRecord(
	string? messageId,
	string from,
	string subject,
	DateTimeOffset date,
	string bodyText,
	string bodySource,
	string memberEmail
)
{
	public string? MessageId { get; } = messageId;
	public string From { get; } = from;
	public string Subject { get; } = subject;
	public DateTimeOffset Date { get; } = date;
	public string BodyText { get; } = bodyText;
	public string BodySource { get; } = bodySource; // "text" | "html" | "none"
	public string MemberEmail { get; } = memberEmail;

	public ReceiptStatus Status { get; set; } = ReceiptStatus.Queued;
	public ExtractedReceipt? Extracted { get; set; }
	public int Attempts { get; set; }
	public bool Reconciled { get; set; }
	public string? Error { get; set; } // TODO: Add Error types
	public DateTimeOffset EnqueuedAt { get; init; }
	public DateTimeOffset? CompletedAt { get; set; }
}