namespace Kvitteria.Contracts.DTO;

/// <summary>
/// A mail or upload that could not become a receipt. The mail text, transcription and image are
/// deliberately absent: they are kept only so a retry can reuse them, and never leave the server here.
/// For an upload <see cref="Subject"/> and <see cref="From"/> are empty, <see cref="MailDate"/> is the
/// capture time, and <see cref="Source"/> is <c>Upload</c>.
/// </summary>
public record ReceiptFailureDto(
	Guid Id,
	string Subject,
	string From,
	DateTime MailDate,
	DateTime FailedAt,
	string Error,
	int Attempts,
	Guid MemberId,
	string MemberDisplayName,
	Guid? MailboxId,
	string Source,
	DateTime ReceivedAt,
	bool HasTranscription
);

/// <summary>How much mail sits in each stage, and how many stored receipts did not reconcile.</summary>
public record PipelineStatusDto(
	int Queued,
	int Extracting,
	int Persisting,
	int Failed,
	int Unreconciled,
	DateTime? LastPolledAt,
	int PollIntervalSeconds,
	List<MailboxActivityDto> Mailboxes
);

/// <summary>
/// What a mailbox has produced: when mail last arrived, and how many receipts it contributed in the
/// current calendar month in Danish local time.
/// </summary>
public record MailboxActivityDto(
	Guid MailboxId,
	Guid MemberId,
	string MemberDisplayName,
	string Folder,
	DateTime? LastMessageAt,
	int ReceiptsThisMonth
);
