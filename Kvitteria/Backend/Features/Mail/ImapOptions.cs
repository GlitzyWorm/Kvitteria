using Backend.Abstractions;
namespace Backend.Features.Mail;

public sealed class ImapOptions : IOptionsSection
{
	public static string SectionName => "Imap";

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
