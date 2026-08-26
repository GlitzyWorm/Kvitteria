namespace Kvitteria.Contracts.DTO;

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
