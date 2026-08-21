using Backend.Features.Auth;
namespace Backend.Features.Endpoints.DTO;

/// <summary>
/// What the caller is and what they may do. The frontends use the permission list to decide which
/// controls to render, so it is the effective set — admins get everything.
/// </summary>
public record CurrentUserDto(
	Guid Id,
	string Email,
	string DisplayName,
	string Role,
	string Status,
	List<string> Permissions
);

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
