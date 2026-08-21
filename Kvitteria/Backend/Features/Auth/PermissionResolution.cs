using Backend.Features.Persistence.Model.Users;
namespace Backend.Features.Auth;

/// <summary>What a member may actually do, and what an admin change would do to the household.</summary>
public static class PermissionResolution
{
	private static readonly Permission[] AllPermissions = Enum.GetValues<Permission>();

	/// <summary>
	/// Admins hold everything implicitly, so adding a permission never silently locks them out of it.
	/// Members hold the household-wide grants unless they carry a per-user override.
	/// </summary>
	public static HashSet<Permission> Resolve(UserRole role, IEnumerable<Permission> memberGrants, IEnumerable<Permission>? overrides)
	{
		if (role == UserRole.Admin)
			return [.. AllPermissions];

		return [.. overrides ?? memberGrants];
	}

	/// <summary>
	/// Whether a role or status change would leave the household with no active admin — which would lock
	/// everyone out of user management permanently, since only an admin can hand the role back.
	/// </summary>
	public static bool RemovesLastAdmin(bool targetIsActiveAdmin, int otherActiveAdmins, UserRole newRole, UserStatus newStatus)
	{
		if (!targetIsActiveAdmin || otherActiveAdmins > 0)
			return false;

		return newRole != UserRole.Admin || newStatus != UserStatus.Active;
	}
}
