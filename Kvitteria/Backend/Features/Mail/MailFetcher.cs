using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
namespace Backend.Features.Mail;

public sealed partial class MailFetcher(
	IOptionsMonitor<ImapOptions> options,
	ILogger<MailFetcher> logger)
{
	private const int PreviewLength = 100;

	/// <summary>
	/// Fetches unseen receipt mail, invoking <paramref name="onFetched"/> for each message and
	/// flagging it <c>Seen</c> only once that call has completed. Returns the number handed over.
	/// </summary>
	public async Task<int> FetchUnseenAsync(
		Func<ReceiptRecord, CancellationToken, ValueTask> onFetched,
		int maxMessagesPerPoll,
		CancellationToken ct)
	{
		ImapOptions imap = options.CurrentValue;

		if (imap.Folders.Count == 0)
		{
			throw new InvalidOperationException(
				"Imap:Folders is empty or contains an entry without a Folder name; configure one " +
				"entry per member's receipt folder (e.g. INBOX.Netto).");
		}

		var fetched = 0;

		foreach (ImapFolderOptions folderOptions in imap.Folders.Where(CheckIfValid))
		{
			// One client per folder: each entry is a separate account.
			using var client = new ImapClient();

			try
			{
				await client.ConnectAsync(folderOptions.Host, folderOptions.Port, folderOptions.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
				await client.AuthenticateAsync(folderOptions.Username, folderOptions.Password, ct);

				IMailFolder folder = await OpenFolderAsync(client, folderOptions.Folder, ct);

				try
				{
					fetched += await FetchFolderAsync(folder, folderOptions, onFetched, maxMessagesPerPoll-fetched, ct);
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

		return fetched;
	}

	private async Task<int> FetchFolderAsync(
		IMailFolder folder,
		ImapFolderOptions folderOptions,
		Func<ReceiptRecord, CancellationToken, ValueTask> onFetched,
		int remainingBudget,
		CancellationToken ct)
	{
		if (remainingBudget <= 0)
			return 0;

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
			string bodyText = ExtractBody(message, out string bodySource);

			var record = new ReceiptRecord(
				message.MessageId,
				message.From.ToString(),
				message.Subject ?? "",
				message.Date,
				bodyText,
				bodySource,
				folderOptions.MemberEmail
			) { EnqueuedAt = DateTimeOffset.UtcNow };

			await onFetched(record, ct);

			await folder.AddFlagsAsync(id, MessageFlags.Seen, true, ct);
			fetched++;

			if (logger.IsEnabled(LogLevel.Debug))
			{
				logger.LogDebug(
					"Fetched receipt mail from {Folder} for {Member}: subject='{Subject}', from='{From}', date={Date:O}, bodySource={BodySource}, preview: {Preview}",
					folder.FullName, folderOptions.MemberName, message.Subject, message.From, message.Date, bodySource,
					bodyText.Length > PreviewLength ? string.Concat(bodyText.AsSpan(0, PreviewLength), "...") : bodyText);
			}
		}

		return fetched;
	}

	private bool CheckIfValid(ImapFolderOptions folder)
	{
		if (folder.IsComplete())
			return true;
		
		logger.LogWarning(
			"Skipping Imap:Folders entry (member '{Member}', folder '{Folder}'): Host, Username, Password or Folder is missing.",
			folder.MemberName, folder.Folder);
				
		return false;
	}

	private static async Task<IMailFolder> OpenFolderAsync(ImapClient client, string name, CancellationToken ct)
	{
		IMailFolder folder;

		try
		{
			folder = await client.GetFolderAsync(name, ct);
		} catch (FolderNotFoundException)
		{
			List<string> available = [];
			foreach (FolderNamespace ns in client.PersonalNamespaces)
			{
				available.AddRange(from f in await client.GetFoldersAsync(ns, cancellationToken:ct) select f.FullName);
			}

			throw new InvalidOperationException(
				$"IMAP folder '{name}' was not found. Available folders: {string.Join(", ", available)}");
		}

		await folder.OpenAsync(FolderAccess.ReadWrite, ct);
		return folder;
	}
	
	private string ExtractBody(MimeMessage message, out string bodySource)
	{
		if (!string.IsNullOrWhiteSpace(message.TextBody))
		{
			bodySource = "text";
			return NormalizeWhitespace(message.TextBody);
		}

		if (!string.IsNullOrWhiteSpace(message.HtmlBody))
		{
			bodySource = "html";
			return NormalizeWhitespace(HtmlToPlainText.Convert(message.HtmlBody));
		}
		
		logger.LogWarning("Message '{Subject}' has no text or HTML body; skipping body extraction", message.Subject);
		bodySource = "none";
		return "";
	}
	
	private static string NormalizeWhitespace(string text)
	{
		string[] lines = text.ReplaceLineEndings("\n").Split('\n');
		string joined = string.Join('\n', lines.Select(l => l.TrimEnd()));
		return ExcessNewlines().Replace(joined, "\n\n").Trim();
	}

	[GeneratedRegex(@"\n{3,}")]
	private static partial Regex ExcessNewlines();
}