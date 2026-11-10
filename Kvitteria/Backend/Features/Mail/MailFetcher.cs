using Backend.Features.Persistence;
using Backend.Features.Persistence.Factory;
using Backend.Features.Persistence.Model;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;
namespace Backend.Features.Mail;

/// <summary>
/// One poll's worth of work. <paramref name="Attempted"/> and <paramref name="Failed"/> let the caller
/// tell a mailbox that is broken apart from a household that has none configured, and only back off in
/// the first case.
/// </summary>
public sealed record MailPollResult(int Fetched, int Attempted, int Failed)
{
	/// <summary>Every mailbox that was tried failed, so the poll achieved nothing and should back off.</summary>
	public bool AllFailed => Attempted > 0 && Failed == Attempted;
}

public sealed partial class MailFetcher(
	IServiceScopeFactory scopeFactory,
	MailboxSecrets secrets,
	ReceiptIntakeStore store,
	ILogger<MailFetcher> logger)
{
	private const int PreviewLength = 100;

	/// <summary>
	/// Fetches unseen receipt mail from every enabled mailbox, invoking <paramref name="onFetched"/> for
	/// each message and flagging it <c>Seen</c> only once that call has completed.
	/// </summary>
	/// <remarks>
	/// Each mailbox is isolated: one unreachable host or wrong password must not starve the others, which
	/// is what happened while a single failure propagated out of the loop. The outcome is recorded on the
	/// row so a frontend can show which mailbox is broken and why.
	/// </remarks>
	public async Task<MailPollResult> FetchUnseenAsync(
		Func<ReceiptRecord, CancellationToken, ValueTask> onFetched,
		int maxMessagesPerPoll,
		CancellationToken ct)
	{
		// MailFetcher is a singleton; the scope holds the tracked mailboxes for the whole poll so the
		// status columns can be written back at the end.
		using IServiceScope scope = scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();

		List<Mailbox> mailboxes = await dbContext.Mailboxes
		                                         .Where(m => m.IsEnabled)
		                                         .Include(m => m.AppUser)
		                                         .OrderBy(m => m.Host)
		                                         .ThenBy(m => m.Username)
		                                         .ToListAsync(ct);

		if (mailboxes.Count == 0)
		{
			// Not an error: an empty table is how every household starts. Throwing here made a fresh
			// install look like a failing poll and drove the worker's backoff for nothing.
			logger.LogInformation("No enabled mailboxes are configured; nothing to poll. Add one with POST /api/mailboxes.");
			return new MailPollResult(0, 0, 0);
		}

		var fetched = 0;
		var attempted = 0;
		var failed = 0;

		foreach (Mailbox mailbox in mailboxes)
		{
			// The per-poll cap is a household budget, not a per-mailbox one. Once it is spent the
			// remaining mailboxes are left for the next poll with their status untouched, rather than
			// being recorded as successfully polled without having been contacted.
			if (fetched >= maxMessagesPerPoll)
			{
				logger.LogInformation(
					"Reached the {Budget}-message poll cap; {Remaining} mailbox(es) wait for the next poll.",
					maxMessagesPerPoll, mailboxes.Count-attempted);
				break;
			}

			attempted++;

			try
			{
				fetched += await FetchMailboxAsync(mailbox, onFetched, maxMessagesPerPoll-fetched, ct);

				mailbox.LastPolledAt = DateTime.UtcNow;
				mailbox.LastError = null;
				mailbox.ConsecutiveFailures = 0;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// Broad on purpose: the next mailbox must still be polled.
				failed++;
				mailbox.LastError = ex.Message;
				mailbox.ConsecutiveFailures++;

				logger.LogWarning(
					ex,
					"Polling mailbox {Username} ({Folder}) failed ({Failures} consecutive); continuing with the others.",
					mailbox.Username, mailbox.Folder, mailbox.ConsecutiveFailures);
			}
		}

		// Status is worth persisting even on shutdown, so this is not skipped when cancelled.
		await dbContext.SaveChangesAsync(CancellationToken.None);

		return new MailPollResult(fetched, attempted, failed);
	}

	private async Task<int> FetchMailboxAsync(
		Mailbox mailbox,
		Func<ReceiptRecord, CancellationToken, ValueTask> onFetched,
		int remainingBudget,
		CancellationToken ct)
	{
		string password = secrets.Unprotect(mailbox.EncryptedPassword);

		// One client per mailbox: each is a separate account.
		using var client = new ImapClient();

		try
		{
			await ImapConnection.ConnectAsync(client, mailbox, password, ct);

			IMailFolder folder = await ImapConnection.OpenFolderAsync(client, mailbox, ct);

			try
			{
				return await FetchFolderAsync(folder, mailbox, onFetched, remainingBudget, ct);
			}
			finally
			{
				if (folder.IsOpen && !ct.IsCancellationRequested)
					await folder.CloseAsync(expunge: false, ct);
			}
		}
		finally
		{
			// Sends LOGOUT, which disposal alone does not.
			// Skipped on shutdown so cancellation cannot block on the network.
			if (client.IsConnected && !ct.IsCancellationRequested)
				await client.DisconnectAsync(quit: true, ct);
		}
	}

	private async Task<int> FetchFolderAsync(
		IMailFolder folder,
		Mailbox mailbox,
		Func<ReceiptRecord, CancellationToken, ValueTask> onFetched,
		int remainingBudget,
		CancellationToken ct)
	{
		IList<UniqueId> uids = await folder.SearchAsync(SearchQuery.NotSeen, ct);
		var fetched = 0;

		if (uids.Count > remainingBudget)
		{
			logger.LogInformation(
				"{Folder} has {Total} unseen messages; taking {Budget} this poll and leaving the rest unread.",
				folder.FullName, uids.Count, remainingBudget);
			uids = uids.Take(remainingBudget).ToList();
		}

		foreach (UniqueId id in uids)
		{
			MimeMessage message = await folder.GetMessageAsync(id, ct);

			// Already stored or in progress: the same mail in two mailboxes, or one marked unread again.
			// Flag it and move on rather than burn an extraction that would only hit the unique index.
			if (message.MessageId is not null && await store.IsKnownAsync(message.MessageId, ct))
			{
				await folder.AddFlagsAsync(id, MessageFlags.Seen, true, ct);
				logger.LogInformation(
					"Skipping '{Subject}' in {Folder}: message {MessageId} is already known.",
					message.Subject, folder.FullName, message.MessageId);
				continue;
			}

			string bodyText = ExtractBody(message, out string bodySource);

			// Recorded before anything else happens to it. If this throws the mail stays unread and is
			// fetched again; once it succeeds the database holds the mail whatever happens next.
			ReceiptRecord? record = await store.RecordAsync(
				message.MessageId,
				message.From.ToString(),
				message.Subject ?? "",
				message.Date,
				bodyText,
				bodySource,
				mailbox,
				ct);

			if (record is null)
			{
				// Lost a race on the unique index to another mailbox's copy.
				await folder.AddFlagsAsync(id, MessageFlags.Seen, true, ct);
				continue;
			}

			// Enqueue before flagging: if the flag then fails, the next poll finds the mail known and
			// flags it, whereas the other order could leave a recorded mail nobody enqueues until restart.
			await onFetched(record, ct);

			await folder.AddFlagsAsync(id, MessageFlags.Seen, true, ct);
			fetched++;

			if (logger.IsEnabled(LogLevel.Debug))
			{
				logger.LogDebug(
					"Fetched receipt mail from {Folder} for {Member}: subject='{Subject}', from='{From}', date={Date:O}, bodySource={BodySource}, preview: {Preview}",
					folder.FullName, mailbox.AppUser?.DisplayName, message.Subject, message.From, message.Date, bodySource,
					bodyText.Length > PreviewLength ? string.Concat(bodyText.AsSpan(0, PreviewLength), "...") : bodyText);
			}
		}

		return fetched;
	}

	private string ExtractBody(MimeMessage message, out string bodySource)
	{
		string body = MailBody.From(message, out bodySource);

		if (bodySource == "none")
			logger.LogWarning("Message '{Subject}' has no text or HTML body; skipping body extraction", message.Subject);

		return body;
	}
}
