namespace Kvitteria.Contracts.DTO;

/// <summary>
/// A mail that could not become a receipt. The mail text is deliberately absent: it is kept only so a
/// retry can reuse it, and never leaves the server.
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
	Guid? MailboxId
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
