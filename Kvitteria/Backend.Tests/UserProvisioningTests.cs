using Backend.Features.Auth;
using Backend.Features.Persistence.Model.Users;
namespace Backend.Tests;

public class UserProvisioningTests
{
	[Fact]
	public void First_authenticated_caller_claims_an_empty_household()
	{
		Assert.Equal(
			ProvisioningAction.Bootstrap,
			UserProvisioning.Decide(matchedById:null, matchedByEmail:null, householdEmpty:true));
	}

	[Fact]
	public void A_stranger_is_rejected_once_the_household_exists()
	{
		Assert.Equal(
			ProvisioningAction.Reject,
			UserProvisioning.Decide(matchedById:null, matchedByEmail:null, householdEmpty:false));
	}

	[Fact]
	public void A_pre_authorized_email_is_bound_on_first_sign_in()
	{
		Assert.Equal(
			ProvisioningAction.Bind,
			UserProvisioning.Decide(matchedById:null, matchedByEmail:UserStatus.Pending, householdEmpty:false));
	}

	[Fact]
	public void An_already_bound_member_is_recognised()
	{
		Assert.Equal(
			ProvisioningAction.Existing,
			UserProvisioning.Decide(matchedById:UserStatus.Active, matchedByEmail:null, householdEmpty:false));
	}

	[Fact]
	public void A_disabled_member_cannot_come_back_in()
	{
		Assert.Equal(
			ProvisioningAction.Reject,
			UserProvisioning.Decide(matchedById:UserStatus.Disabled, matchedByEmail:null, householdEmpty:false));
	}

	[Fact]
	public void A_disabled_pre_authorized_row_is_not_a_way_in_either()
	{
		Assert.Equal(
			ProvisioningAction.Reject,
			UserProvisioning.Decide(matchedById:null, matchedByEmail:UserStatus.Disabled, householdEmpty:false));
	}

	[Fact]
	public void A_bound_row_left_pending_is_activated_rather_than_rejected()
	{
		Assert.Equal(
			ProvisioningAction.Bind,
			UserProvisioning.Decide(matchedById:UserStatus.Pending, matchedByEmail:null, householdEmpty:false));
	}

	/// <summary>
	/// A claimed row identifies its owner; the email is only how an unclaimed row finds one. Preferring the
	/// email match would let a second, pre-authorized row shadow the member's real one.
	/// </summary>
	[Fact]
	public void A_claimed_row_wins_over_a_matching_email()
	{
		Assert.Equal(
			ProvisioningAction.Existing,
			UserProvisioning.Decide(matchedById:UserStatus.Active, matchedByEmail:UserStatus.Pending, householdEmpty:false));
	}

	/// <summary>
	/// Bootstrap must not fire while anyone at all exists, or a second admin appears out of nowhere.
	/// </summary>
	[Fact]
	public void Bootstrap_never_competes_with_an_existing_member()
	{
		Assert.Equal(
			ProvisioningAction.Existing,
			UserProvisioning.Decide(matchedById:UserStatus.Active, matchedByEmail:null, householdEmpty:true));
	}
}
