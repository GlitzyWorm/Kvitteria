using System.Net.Sockets;
using Backend.Features.Persistence.Model;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
namespace Backend.Features.Mail;

/// <summary>
/// The connect / authenticate / open-folder sequence, shared by the poller and by the write endpoints
/// that verify a mailbox before storing it. Both need the same diagnostics — above all the list of
/// folders the server actually has, which is what makes a folder-name typo obvious.
/// </summary>
public static class ImapConnection
{
	/// <summary>
	/// Connects and authenticates. The caller owns disconnection, since the poller keeps the session for
	/// the length of a fetch.
	/// </summary>
	public static async Task ConnectAsync(ImapClient client, Mailbox mailbox, string password, CancellationToken ct)
	{
		SecureSocketOptions socketOptions = mailbox.UseSsl
			? SecureSocketOptions.SslOnConnect
			: SecureSocketOptions.StartTlsWhenAvailable;

		await client.ConnectAsync(mailbox.Host, mailbox.Port, socketOptions, ct);
		await client.AuthenticateAsync(mailbox.Username, password, ct);
	}

	/// <summary>
	/// Opens the mailbox's folder for writing, because the poller has to set the <c>\Seen</c> flag.
	/// </summary>
	public static Task<IMailFolder> OpenFolderAsync(ImapClient client, Mailbox mailbox, CancellationToken ct) =>
		OpenFolderAsync(client, mailbox.Folder, FolderAccess.ReadWrite, ct);

	/// <summary>
	/// Proves a mailbox's settings work — host, TLS, credentials and folder name — and leaves nothing
	/// behind. Returns null on success, or a message fit for a problem detail. The folder is opened
	/// read-only so verifying cannot mark anything seen.
	/// </summary>
	public static async Task<string?> VerifyAsync(Mailbox mailbox, string password, CancellationToken ct)
	{
		using var client = new ImapClient();

		try
		{
			await ConnectAsync(client, mailbox, password, ct);
			IMailFolder folder = await OpenFolderAsync(client, mailbox.Folder, FolderAccess.ReadOnly, ct);
			await folder.CloseAsync(expunge: false, ct);
			return null;
		}
		catch (AuthenticationException ex)
		{
			return $"The server rejected the credentials for '{mailbox.Username}': {ex.Message}";
		}
		catch (SocketException ex)
		{
			return $"Could not reach {mailbox.Host}:{mailbox.Port}: {ex.Message}";
		}
		catch (SslHandshakeException ex)
		{
			return $"TLS handshake with {mailbox.Host}:{mailbox.Port} failed: {ex.Message}";
		}
		catch (ImapProtocolException ex)
		{
			return $"The server at {mailbox.Host}:{mailbox.Port} did not speak IMAP as expected: {ex.Message}";
		}
		// Carries the "Available folders: ..." list, which is the whole point of verifying.
		catch (InvalidOperationException ex)
		{
			return ex.Message;
		}
		finally
		{
			if (client.IsConnected && !ct.IsCancellationRequested)
				await client.DisconnectAsync(quit: true, ct);
		}
	}

	private static async Task<IMailFolder> OpenFolderAsync(
		ImapClient client,
		string name,
		FolderAccess access,
		CancellationToken ct)
	{
		IMailFolder folder;

		try
		{
			folder = await client.GetFolderAsync(name, ct);
		}
		catch (FolderNotFoundException)
		{
			List<string> available = [];
			foreach (FolderNamespace ns in client.PersonalNamespaces)
			{
				available.AddRange(from f in await client.GetFoldersAsync(ns, cancellationToken:ct) select f.FullName);
			}

			throw new InvalidOperationException(
				$"IMAP folder '{name}' was not found. Available folders: {string.Join(", ", available)}");
		}

		await folder.OpenAsync(access, ct);
		return folder;
	}
}
