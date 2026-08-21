using Backend.Features.Persistence.Model.Users;
namespace Backend.Features.Auth;

public enum ProvisioningAction
{
	/// <summary>An active row already belongs to this identity-provider user; nothing to do.</summary>
	Existing,

	/// <summary>Attach the identity-provider user to a waiting row and activate it.</summary>
	Bind,

	/// <summary>The household has no members yet, so this caller becomes its admin.</summary>
	Bootstrap,

	/// <summary>Authenticated, but not a member of this household.</summary>
	Reject
}

/// <summary>
/// Decides what an authenticated caller is, on every request. Bootstrapping the first admin and binding
/// a pre-authorized email are the same question asked at different points in the household's life, so
/// they are one decision rather than two flows that can disagree.
/// </summary>
public static class UserProvisioning
{
	/// <param name="matchedById">Status of the row already claimed by this identity-provider user, if any.</param>
	/// <param name="matchedByEmail">
	/// Status of an unclaimed row pre-authorizing this email, if any.
	/// </param>
	/// <param name="householdEmpty">Whether any member exists at all.</param>
	public static ProvisioningAction Decide(UserStatus? matchedById, UserStatus? matchedByEmail, bool householdEmpty)
	{
		// A claimed row wins over anything else: once bound, the email is no longer how we identify them.
		if (matchedById.HasValue)
			return matchedById.Value switch {
				UserStatus.Active   => ProvisioningAction.Existing,
				UserStatus.Pending  => ProvisioningAction.Bind,
				_                   => ProvisioningAction.Reject
			};

		if (matchedByEmail.HasValue)
			return matchedByEmail.Value == UserStatus.Disabled
				? ProvisioningAction.Reject
				: ProvisioningAction.Bind;

		// First one through the door owns the household. Nobody can be pre-authorized before an admin
		// exists to do it, so this is the only way in.
		return householdEmpty
			? ProvisioningAction.Bootstrap
			: ProvisioningAction.Reject;
	}
}
