using Backend.Features.Persistence.Model;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

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
