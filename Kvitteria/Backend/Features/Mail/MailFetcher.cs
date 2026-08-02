using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using Backend.Utils;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
namespace Backend.Features.Mail;

public sealed partial class MailFetcher(
	IOptionsMonitor<ImapOptions> options,
	ILogger<MailFetcher> logger)
{
	private readonly HtmlToText _converter = new HtmlToText();
	
	public async Task<IReadOnlyList<ReceiptEmail>> FetchUnseenAsync(CancellationToken ct)
	{
		ImapOptions imap = options.CurrentValue;

		if (imap.Folders.Count == 0)
		{
			throw new InvalidOperationException(
				"Imap:Folders is empty or contains an entry without a Folder name; configure one " +
				"entry per member's receipt folder (e.g. INBOX.Netto).");
		}

		using var client = new ImapClient();
		List<ReceiptEmail> results = [];
		
		foreach (ImapFolderOptions folderOptions in imap.Folders.Where(CheckIfValid))
		{
			await client.ConnectAsync(folderOptions.Host, folderOptions.Port, folderOptions.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
			await client.AuthenticateAsync(folderOptions.Username, folderOptions.Password, ct);
			
			IMailFolder folder = await OpenFolderAsync(client, folderOptions.Folder, ct);
			IList<UniqueId> uids = await folder.SearchAsync(SearchQuery.NotSeen, ct);
			
			foreach (UniqueId id in uids)
			{
				MimeMessage message = await folder.GetMessageAsync(id, ct);
				string bodyText = ExtractBody(message, out string bodySource);
				
				results.Add(new ReceiptEmail(
					message.MessageId,
					message.From.ToString(),
					message.Subject ?? "",
					message.Date,
					bodyText,
					bodySource,
					folderOptions.MemberEmail
				));
				
				await folder.AddFlagsAsync(id, MessageFlags.Seen, true, ct);
				
				logger.LogDebug(
					"Fetched receipt mail from {Folder} for {Member}: subject='{Subject}', from='{From}', date={Date:O}, bodySource={BodySource}, preview: {Preview}",
					folder.FullName, folderOptions.MemberName, message.Subject, message.From, message.Date, bodySource,
					bodyText.Length > 100 ? string.Concat(bodyText.AsSpan(0, 100), "...") : bodyText);
			}
			
			await folder.CloseAsync(expunge: false, ct);
		}
		
		await client.DisconnectAsync(quit: true, ct);
		return results;
	}

	private bool CheckIfValid(ImapFolderOptions folder)
	{
		if (!string.IsNullOrWhiteSpace(folder.Host) &&
		    !string.IsNullOrWhiteSpace(folder.Username) &&
		    !string.IsNullOrWhiteSpace(folder.Password)) return true;
		
		logger.LogWarning(
			"Imap configuration is missing Host, Username or Password — " +
			"did you copy appsettings.Local.json.example to appsettings.Local.json and fill it in?");
				
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
			return NormalizeWhitespace(_converter.Convert(message.HtmlBody));
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