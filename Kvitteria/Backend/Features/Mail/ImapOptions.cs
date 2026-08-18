using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Mail;

public sealed class ImapOptions : IOptionsSection
{
	public static string SectionName => "Imap";

	[Range(1, 86_400)]
	public int PollIntervalSeconds { get; set; } = 60;

	/// <summary>
	/// Safety valve on a large unseen backlog. With bounded-channel backpressure, an unbounded first
	/// poll would otherwise hold the IMAP session open until the server timed it out.
	/// </summary>
	[Range(1, 1_000)]
	public int MaxMessagesPerPoll { get; set; } = 25;

	/// <summary>One entry per household member's receipt folder.</summary>
	public List<ImapFolderOptions> Folders { get; set; } = [];
}

public sealed class ImapFolderOptions
{
	public string Host { get; set; } = "";
	public int Port { get; set; } = 993;
	public bool UseSsl { get; set; } = true;
	public string Username { get; set; } = "";
	public string Password { get; set; } = "";
	public string Folder { get; set; } = "";
	public string MemberName { get; set; } = "";
	public string MemberEmail { get; set; } = "";

	/// <summary>
	/// ValidateDataAnnotations does not recurse into collection elements, so per-entry completeness is
	/// checked here and reported by MailFetcher, which can name the offending member.
	/// </summary>
	public bool IsComplete() =>
		!string.IsNullOrWhiteSpace(Host) &&
		!string.IsNullOrWhiteSpace(Username) &&
		!string.IsNullOrWhiteSpace(Password) &&
		!string.IsNullOrWhiteSpace(Folder);
}
