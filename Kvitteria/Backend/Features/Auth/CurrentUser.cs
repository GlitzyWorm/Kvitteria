using Backend.Features.Persistence.Model.Users;
namespace Backend.Features.Auth;

/// <summary>The resolved household member behind the current request, and what they may do.</summary>
public sealed record CurrentUser(
	Guid Id,
	Guid SupabaseUserId,
	string Email,
	string DisplayName,
	UserRole Role,
	UserStatus Status,
	HashSet<Permission> Permissions
)
{
	public bool Has(Permission permission) => Permissions.Contains(permission);
}
