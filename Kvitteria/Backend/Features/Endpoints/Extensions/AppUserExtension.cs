using Backend.Features.Persistence.Model.Users;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class AppUserDtoExtension
{
	public static AppUserDto ToDto(this AppUser user)
	{
		return new AppUserDto(
			user.Id,
			user.Email,
			user.DisplayName,
			user.Role.ToString(),
			user.Status.ToString(),
			user.AuthUserId != null,
			user.CreatedAt,
			user.LastSeenAt
		);
	}
}
