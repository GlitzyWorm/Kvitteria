using Backend.Features.Auth;
namespace Backend.Features.Persistence.Model.Users;

public enum UserRole
{
	/// <summary>Holds every permission implicitly, so a new permission never locks the admin out of it.</summary>
	Admin,
	Member
}

public enum UserStatus
{
	/// <summary>Pre-authorized by an admin, but nobody has signed in as this email yet.</summary>
	Pending,
	Active,

	/// <summary>Revoked. Preferred over deletion, which would orphan whatever the user touched.</summary>
	Disabled
}

/// <summary>
/// A household member, as this application knows them. Identity itself lives in GoTrue; the only
/// link is <see cref="AuthUserId"/>, deliberately not a foreign key, so auth can move to a hosted
/// project without touching this schema.
/// </summary>
public sealed class AppUser
{
	public Guid Id { get; set; }

	/// <summary>
	/// The <c>sub</c> claim of the identity-provider user that claimed this row. Null while the row is only a
	/// pre-authorized email waiting for its owner to sign up.
	/// </summary>
	public Guid? AuthUserId { get; set; }

	public required string Email { get; set; }
	public required string DisplayName { get; set; }
	public UserRole Role { get; set; }
	public UserStatus Status { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? LastSeenAt { get; set; }

	/// <summary>
	/// Per-user deviation from the household-wide member grants. Nothing writes this yet; the column
	/// exists so per-user overrides can be added without a migration.
	/// </summary>
	public List<Permission>? PermissionOverrides { get; set; }
}
