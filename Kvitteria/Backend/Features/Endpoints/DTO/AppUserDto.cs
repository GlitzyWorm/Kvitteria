using Backend.Features.Persistence.Model.Users;
namespace Backend.Features.Endpoints.DTO;

/// <summary>
/// Role and status travel as names, matching how they are stored, so a client never depends on enum
/// ordering.
/// </summary>
public record AppUserDto(
	Guid Id,
	string Email,
	string DisplayName,
	string Role,
	string Status,
	bool HasSignedIn,
	DateTime CreatedAt,
	DateTime? LastSeenAt
);

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
			user.SupabaseUserId != null,
			user.CreatedAt,
			user.LastSeenAt
		);
	}
}
