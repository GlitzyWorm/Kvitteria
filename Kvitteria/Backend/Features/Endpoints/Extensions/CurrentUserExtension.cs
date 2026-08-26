using Backend.Features.Auth;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class CurrentUserDtoExtension
{
	public static CurrentUserDto ToDto(this CurrentUser user)
	{
		return new CurrentUserDto(
			user.Id,
			user.Email,
			user.DisplayName,
			user.Role.ToString(),
			user.Status.ToString(),
			user.Permissions.OrderBy(permission => permission).ToNames()
		);
	}
}
