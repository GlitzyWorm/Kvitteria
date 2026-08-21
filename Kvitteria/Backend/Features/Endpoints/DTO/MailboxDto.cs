using Backend.Features.Persistence.Model;
namespace Backend.Features.Endpoints.DTO;

/// <summary>
/// A mailbox as clients see it. The password is never part of this — <see cref="HasPassword"/> is all a
/// UI needs to render "stored" beside the field.
/// </summary>
public record MailboxDto(
	Guid Id,
	Guid AppUserId,
	string MemberDisplayName,
	string MemberEmail,
	string Host,
	int Port,
	bool UseSsl,
	string Username,
	string Folder,
	bool IsEnabled,
	bool HasPassword,
	DateTime? LastPolledAt,
	string? LastError,
	int ConsecutiveFailures,
	DateTime CreatedAt
);

public record CreateMailboxDto(
	Guid AppUserId,
	string Host,
	int Port,
	bool UseSsl,
	string Username,
	string Password,
	string Folder,
	bool IsEnabled
);

/// <summary>
/// <see cref="Password"/> is nullable and means "leave the stored one alone", so a client can toggle
/// <see cref="IsEnabled"/> or fix a folder name without having to hold the password it can never read.
/// </summary>
public record PutMailboxDto(
	Guid AppUserId,
	string Host,
	int Port,
	bool UseSsl,
	string Username,
	string? Password,
	string Folder,
	bool IsEnabled
);

public static class MailboxDtoExtension
{
	public static MailboxDto ToDto(this Mailbox mailbox) =>
		new(
			mailbox.Id,
			mailbox.AppUserId,
			mailbox.AppUser == null ? "" : mailbox.AppUser.DisplayName,
			mailbox.AppUser == null ? "" : mailbox.AppUser.Email,
			mailbox.Host,
			mailbox.Port,
			mailbox.UseSsl,
			mailbox.Username,
			mailbox.Folder,
			mailbox.IsEnabled,
			mailbox.EncryptedPassword.Length > 0,
			mailbox.LastPolledAt,
			mailbox.LastError,
			mailbox.ConsecutiveFailures,
			mailbox.CreatedAt);
}
