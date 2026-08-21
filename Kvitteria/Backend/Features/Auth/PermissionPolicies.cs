using Microsoft.AspNetCore.Authorization;
namespace Backend.Features.Auth;

/// <summary>Requires a resolved, active household member and nothing more. Gates every read.</summary>
public sealed class ActiveUserRequirement : IAuthorizationRequirement;

public sealed class PermissionRequirement(Permission permission) : IAuthorizationRequirement
{
	public Permission Permission => permission;
}

public static class PermissionPolicies
{
	/// <summary>Authenticated and a member in good standing; carries no write capability.</summary>
	public const string ActiveUser = "active-user";

	public static string Name(Permission permission) => $"perm:{permission}";

	extension(AuthorizationOptions options)
	{
		/// <summary>
		/// One policy per permission, registered mechanically so a new enum value cannot be forgotten
		/// here.
		/// </summary>
		internal void AddHouseholdPolicies()
		{
			options.AddPolicy(ActiveUser, policy => policy
			                                        .RequireAuthenticatedUser()
			                                        .AddRequirements(new ActiveUserRequirement()));

			foreach (Permission permission in Enum.GetValues<Permission>())
			{
				options.AddPolicy(Name(permission), policy => policy
				                                              .RequireAuthenticatedUser()
				                                              .AddRequirements(new PermissionRequirement(permission)));
			}
		}
	}
}

/// <summary>
/// Resolves the caller once and settles every household requirement from that one answer, rather than
/// re-resolving per requirement.
/// </summary>
internal sealed class HouseholdAuthorizationHandler(CurrentUserService currentUserService, IHttpContextAccessor httpContextAccessor)
	: IAuthorizationHandler
{
	public async Task HandleAsync(AuthorizationHandlerContext context)
	{
		List<IAuthorizationRequirement> household = [
			.. context.PendingRequirements.Where(requirement => requirement is ActiveUserRequirement or PermissionRequirement)
		];

		if (household.Count == 0)
			return;

		CancellationToken ct = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
		CurrentUser? user = await currentUserService.ResolveAsync(context.User, ct);

		// Not a member: leave every requirement unmet, which the framework turns into a 403.
		if (user == null)
			return;

		foreach (IAuthorizationRequirement requirement in household)
		{
			switch (requirement)
			{
				case ActiveUserRequirement:
					context.Succeed(requirement);
					break;

				case PermissionRequirement permission when user.Has(permission.Permission):
					context.Succeed(requirement);
					break;
			}
		}
	}
}
