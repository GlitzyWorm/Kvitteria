namespace Kvitteria.Contracts.DTO;

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