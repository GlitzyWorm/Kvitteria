using Backend.Features.Auth;
namespace Backend.Features.Persistence.Model.Users;

/// <summary>
/// Household-wide configuration. Exactly one row exists — the scope is one household, with no option to
/// create more — so an admin's edit is a single atomic update rather than a fan-out over users.
/// </summary>
public sealed class HouseholdSettings
{
	public Guid Id { get; set; }

	/// <summary>What every <see cref="UserRole.Member"/> may do. Admins are unaffected.</summary>
	public List<Permission> MemberPermissions { get; set; } = [];
}
