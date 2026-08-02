using Backend.Abstractions;
namespace Backend.Features.Mail;

public sealed class ImapOptions : IOptions
{
	public static string SectionName => "Imap";
	
	public int PollIntervalSeconds { get; set; } = 60;
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
}