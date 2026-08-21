using System.Security.Claims;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Auth;

/// <summary>
/// Turns a validated access token into a household member, provisioning one where
/// <see cref="UserProvisioning"/> says it should. Scoped, and memoized for the request, because several
/// authorization requirements can be evaluated for a single call.
/// </summary>
public sealed class CurrentUserService(
	KvitteriaDbContext dbContext,
	PermissionCache cache,
	ILogger<CurrentUserService> logger
)
{
	/// <summary>
	/// Serializes the one moment in the household's life when the first member is created. Without it two
	/// concurrent first requests both see an empty table and both become admin.
	/// </summary>
	private const long BootstrapLockKey = 0x4B56_4954; // "KVIT"

	private CurrentUser? resolved;
	private bool attempted;

	/// <summary>Null when the caller is not, and may not become, a member of this household.</summary>
	public async Task<CurrentUser?> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
	{
		if (attempted)
			return resolved;

		attempted = true;
		resolved = await ResolveUncachedAsync(principal, ct);
		return resolved;
	}

	/// <summary>
	/// For a check that cannot be a route policy because it depends on the request body — see
	/// <c>SetAsDefault</c> on the line category and meal endpoints, which reaches past the line into the
	/// canonical item's defaults.
	/// </summary>
	public async Task<bool> HasAsync(ClaimsPrincipal principal, Permission permission, CancellationToken ct)
	{
		CurrentUser? user = await ResolveAsync(principal, ct);
		return user != null && user.Has(permission);
	}

	private async Task<CurrentUser?> ResolveUncachedAsync(ClaimsPrincipal principal, CancellationToken ct)
	{
		// Authorization still runs when authentication failed, and "no token" is not a claims problem.
		if (principal.Identity?.IsAuthenticated != true)
			return null;

		// MapInboundClaims is off, so these are the raw JWT claim names GoTrue issues.
		string? subject = principal.FindFirstValue("sub");
		string? email = principal.FindFirstValue("email");

		if (!Guid.TryParse(subject, out Guid authUserId) || string.IsNullOrWhiteSpace(email))
		{
			logger.LogWarning("Token is missing a usable sub or email claim; treating as not a member.");
			return null;
		}

		if (cache.TryGet(authUserId, out CurrentUser? cached))
			return cached;

		Candidates candidates = await FindCandidatesAsync(authUserId, email, ct);

		// Bootstrap is the only branch that races, so it is the only one that pays for a lock.
		if (candidates.Action != ProvisioningAction.Bootstrap)
			return await ApplyAsync(candidates, authUserId, email, ct);

		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(ct);
		await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [BootstrapLockKey], ct);

		candidates = await FindCandidatesAsync(authUserId, email, ct);
		CurrentUser? user = await ApplyAsync(candidates, authUserId, email, ct);

		await transaction.CommitAsync(ct);
		return user;
	}

	private async Task<Candidates> FindCandidatesAsync(Guid authUserId, string email, CancellationToken ct)
	{
		AppUser? byId = await dbContext.AppUsers
		                               .FirstOrDefaultAsync(u => u.AuthUserId == authUserId, ct);

		// Only an unclaimed row can be bound; a claimed one belongs to whoever claimed it.
		AppUser? byEmail = byId != null
			? null
			: await dbContext.AppUsers
			                 .FirstOrDefaultAsync(u => u.AuthUserId == null && u.Email == email, ct);

		bool householdEmpty = byId == null && byEmail == null && !await dbContext.AppUsers.AnyAsync(ct);

		return new Candidates(
			UserProvisioning.Decide(byId?.Status, byEmail?.Status, householdEmpty),
			byId,
			byEmail);
	}

	private async Task<CurrentUser?> ApplyAsync(Candidates candidates, Guid authUserId, string email, CancellationToken ct)
	{
		AppUser? user;

		switch (candidates.Action)
		{
			case ProvisioningAction.Existing:
				user = candidates.ById!;
				break;

			case ProvisioningAction.Bind:
				user = candidates.ById ?? candidates.ByEmail!;
				user.AuthUserId = authUserId;
				user.Status = UserStatus.Active;
				logger.LogInformation("Bound pre-authorized member {Email} to identity {AuthUserId}.", user.Email, authUserId);
				break;

			case ProvisioningAction.Bootstrap:
				user = new AppUser {
					AuthUserId = authUserId,
					Email = email,
					DisplayName = DisplayNameFrom(email),
					Role = UserRole.Admin,
					Status = UserStatus.Active,
					CreatedAt = DateTime.UtcNow
				};
				dbContext.AppUsers.Add(user);
				logger.LogWarning("Household had no members; {Email} claimed it as admin.", email);
				break;

			default:
				logger.LogInformation("Rejected authenticated caller {Email}: not a member of this household.", email);
				return null;
		}

		user.LastSeenAt = DateTime.UtcNow;
		await dbContext.SaveChangesAsync(ct);

		List<Permission> memberGrants = await MemberGrantsAsync(ct);

		var current = new CurrentUser(
			user.Id,
			authUserId,
			user.Email,
			user.DisplayName,
			user.Role,
			user.Status,
			PermissionResolution.Resolve(user.Role, memberGrants, user.PermissionOverrides));

		cache.Set(current);
		return current;
	}

	private async Task<List<Permission>> MemberGrantsAsync(CancellationToken ct)
	{
		HouseholdSettings? settings = await dbContext.HouseholdSettings.AsNoTracking().FirstOrDefaultAsync(ct);
		return settings?.MemberPermissions ?? [];
	}

	/// <summary>GoTrue has no name to offer when signing up with email and password.</summary>
	private static string DisplayNameFrom(string email)
	{
		int at = email.IndexOf('@', StringComparison.Ordinal);
		return at > 0 ? email[..at] : email;
	}

	private sealed record Candidates(ProvisioningAction Action, AppUser? ById, AppUser? ByEmail);
}
