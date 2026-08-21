using Backend.Features.Endpoints.DTO;
namespace Backend.Features.Shared;

/// <summary>
/// Checks a mailbox write for the completeness that <c>ImapFolderOptions.IsComplete()</c> used to
/// report from inside the poller. Enforcing it here means a mailbox that could never work is rejected
/// at the point someone submits it, rather than warned about once a minute forever.
/// </summary>
public static class MailboxValidation
{
	/// <summary>The IANA range. Not restricted to 143/993, since a household may run its own server.</summary>
	private const int MinPort = 1;
	private const int MaxPort = 65_535;

	public static bool TryValidate(CreateMailboxDto dto, out IResult problem)
	{
		// A password cannot be inherited on create: there is nothing stored to fall back to.
		string? error =
			Common(dto.AppUserId, dto.Host, dto.Port, dto.Username, dto.Folder) ??
			Required(dto.Password, nameof(dto.Password));

		return Result(error, out problem);
	}

	public static bool TryValidate(PutMailboxDto dto, out IResult problem)
	{
		// A null password keeps the stored one; a blank string is a client sending an empty field it
		// meant to leave alone, and would silently replace a working password with nothing.
		string? error =
			Common(dto.AppUserId, dto.Host, dto.Port, dto.Username, dto.Folder) ??
			(dto.Password != null && string.IsNullOrWhiteSpace(dto.Password)
				? $"{nameof(dto.Password)} must either be omitted to keep the stored password, or contain a new one."
				: null);

		return Result(error, out problem);
	}

	private static string? Common(Guid appUserId, string host, int port, string username, string folder) =>
		(appUserId == Guid.Empty ? "AppUserId is required; a mailbox belongs to a household member." : null) ??
		Required(host, "Host") ??
		Required(username, "Username") ??
		Required(folder, "Folder") ??
		(port is >= MinPort and <= MaxPort ? null : $"Port must be between {MinPort} and {MaxPort}.");

	private static string? Required(string? value, string name) =>
		string.IsNullOrWhiteSpace(value) ? $"{name} is required." : null;

	private static bool Result(string? error, out IResult problem)
	{
		if (error == null)
		{
			problem = Results.Empty;
			return true;
		}

		problem = Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: error);
		return false;
	}
}
