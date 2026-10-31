using Backend.Features.Mail;
using Backend.Features.Persistence.Model;
using Backend.Features.Shared;
using Kvitteria.Contracts.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Persistence.Factory;

/// <summary>
/// The durable side of the pipeline: every state change of a <see cref="ReceiptIntake"/> row. The
/// queues move work, this is the source of truth.
/// </summary>
/// <remarks>
/// A singleton, so every call opens its own scope. Each transition is a conditional update on the
/// expected status, which makes a duplicate enqueue, a retry racing recovery, or a late write from a
/// worker that lost its row all harmless: the update simply changes nothing.
/// </remarks>
public sealed class ReceiptIntakeStore(IServiceScopeFactory scopeFactory)
{
	/// <summary>Whether a receipt or an unresolved mail already carries this message ID.</summary>
	public async Task<bool> IsKnownAsync(string messageId, CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();

		return await dbContext.Receipts.AnyAsync(r => r.SourceMessageId == messageId, ct)
		       || await dbContext.ReceiptIntakes.AnyAsync(i => i.SourceMessageId == messageId, ct);
	}

	/// <summary>
	/// Records a fetched mail as queued and stamps the mailbox's last-received time. Returns null when
	/// the message ID is already recorded, which the unique index decides rather than a preceding query.
	/// </summary>
	public async Task<ReceiptRecord?> RecordAsync(
		string? messageId,
		string from,
		string subject,
		DateTimeOffset date,
		string bodyText,
		string bodySource,
		Mailbox mailbox,
		CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();

		DateTime now = DateTime.UtcNow;

		var intake = new ReceiptIntake {
			SourceMessageId = messageId,
			From = from,
			Subject = subject,
			MailDate = date.UtcDateTime,
			// Postgres text cannot hold NUL; one such mail would otherwise fail its mailbox on every poll.
			BodyText = bodyText.Replace("\0", ""),
			BodySource = bodySource,
			MailboxId = mailbox.Id,
			AppUserId = mailbox.AppUserId,
			Status = ReceiptStatus.Queued,
			ReceivedAt = now
		};

		dbContext.ReceiptIntakes.Add(intake);

		if (!await dbContext.TrySaveChangesAsync(ct))
			return null;

		await dbContext.Mailboxes
		               .Where(m => m.Id == mailbox.Id)
		               .ExecuteUpdateAsync(s => s.SetProperty(m => m.LastMessageAt, now), ct);

		return ToRecord(intake);
	}

	/// <summary>
	/// Puts anything a previous process left mid-flight back to <c>Queued</c> and returns every queued
	/// mail, oldest first. Assumes a single backend process: it cannot tell another instance's work from
	/// an orphan.
	/// </summary>
	public async Task<List<ReceiptRecord>> RecoverAsync(CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();

		await dbContext.ReceiptIntakes
		               .Where(i => i.Status == ReceiptStatus.Extracting || i.Status == ReceiptStatus.Extracted)
		               .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, ReceiptStatus.Queued), ct);

		List<ReceiptIntake> queued = await dbContext.ReceiptIntakes
		                                            .AsNoTracking()
		                                            .Where(i => i.Status == ReceiptStatus.Queued)
		                                            .OrderBy(i => i.ReceivedAt)
		                                            .ToListAsync(ct);

		return queued.Select(ToRecord).ToList();
	}

	/// <summary>The mail as a pipeline record, or null when the row is gone.</summary>
	public async Task<ReceiptRecord?> LoadAsync(Guid id, CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();

		ReceiptIntake? intake = await dbContext.ReceiptIntakes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);

		return intake is null ? null : ToRecord(intake);
	}

	/// <summary>Queued → Extracting. False means someone else holds it or it no longer exists.</summary>
	public Task<bool> ClaimAsync(Guid id, CancellationToken ct) =>
		TransitionAsync(id, [ReceiptStatus.Queued], s => s.SetProperty(i => i.Status, ReceiptStatus.Extracting), ct);

	/// <summary>Extracting → Extracted.</summary>
	public Task<bool> MarkExtractedAsync(Guid id, int attempts, CancellationToken ct) =>
		TransitionAsync(
			id, [ReceiptStatus.Extracting],
			s => s.SetProperty(i => i.Status, ReceiptStatus.Extracted).SetProperty(i => i.Attempts, attempts), ct);

	/// <summary>Any in-progress stage → Failed, keeping the reason and the time.</summary>
	public Task<bool> MarkFailedAsync(Guid id, string error, int attempts, CancellationToken ct) =>
		TransitionAsync(
			id, [ReceiptStatus.Queued, ReceiptStatus.Extracting, ReceiptStatus.Extracted],
			s => s.SetProperty(i => i.Status, ReceiptStatus.Failed)
			      .SetProperty(i => i.Error, error)
			      .SetProperty(i => i.Attempts, attempts)
			      .SetProperty(i => i.FailedAt, DateTime.UtcNow), ct);

	/// <summary>Failed → Queued, clearing the previous failure.</summary>
	public Task<bool> RequeueFailedAsync(Guid id, CancellationToken ct) =>
		TransitionAsync(
			id, [ReceiptStatus.Failed],
			s => s.SetProperty(i => i.Status, ReceiptStatus.Queued)
			      .SetProperty(i => i.Error, (string?)null)
			      .SetProperty(i => i.FailedAt, (DateTime?)null), ct);

	/// <summary>Queued → Failed again, for a retry the queue could not take.</summary>
	public Task<bool> RestoreFailedAsync(Guid id, string error, DateTime? failedAt, CancellationToken ct) =>
		TransitionAsync(
			id, [ReceiptStatus.Queued],
			s => s.SetProperty(i => i.Status, ReceiptStatus.Failed)
			      .SetProperty(i => i.Error, error)
			      .SetProperty(i => i.FailedAt, failedAt), ct);

	private async Task<bool> TransitionAsync(
		Guid id,
		ReceiptStatus[] from,
		Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<ReceiptIntake>> setters,
		CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();

		int changed = await dbContext.ReceiptIntakes
		                             .Where(i => i.Id == id && from.Contains(i.Status))
		                             .ExecuteUpdateAsync(setters, ct);

		return changed == 1;
	}

	private static ReceiptRecord ToRecord(ReceiptIntake i) =>
		new(
			i.Id,
			i.MailboxId,
			i.SourceMessageId,
			i.From,
			i.Subject,
			new DateTimeOffset(DateTime.SpecifyKind(i.MailDate, DateTimeKind.Utc)),
			i.BodyText,
			i.BodySource,
			i.AppUserId
		) { EnqueuedAt = DateTimeOffset.UtcNow };
}
