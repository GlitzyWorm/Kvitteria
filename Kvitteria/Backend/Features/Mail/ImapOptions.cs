using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Mail;

public sealed class ImapOptions : IOptionsSection
{
	public static string SectionName => "Imap";

	[Range(1, 86_400)]
	public int PollIntervalSeconds { get; set; } = 60;

	/// <summary>Caps one poll so a large unseen backlog cannot hold the IMAP session open indefinitely.</summary>
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
	/// ValidateDataAnnotations does not recurse into collection elements, so entries are checked here.
	/// </summary>
	public bool IsComplete() =>
		!string.IsNullOrWhiteSpace(Host) &&
		!string.IsNullOrWhiteSpace(Username) &&
		!string.IsNullOrWhiteSpace(Password) &&
		!string.IsNullOrWhiteSpace(Folder);
}
