using Backend.Features.Auth;
using Backend.Features.Persistence.Model.Users;
namespace Backend.Tests;

public class PermissionResolutionTests
{
	[Fact]
	public void An_admin_holds_every_permission_including_ones_added_later()
	{
		HashSet<Permission> permissions = PermissionResolution.Resolve(UserRole.Admin, [], overrides:null);

		Assert.Equal(Enum.GetValues<Permission>().Length, permissions.Count);
		Assert.All(Enum.GetValues<Permission>(), permission => Assert.Contains(permission, permissions));
	}

	[Fact]
	public void An_admins_permissions_do_not_depend_on_the_member_grants()
	{
		HashSet<Permission> permissions = PermissionResolution.Resolve(
			UserRole.Admin,
			memberGrants:[],
			overrides:[]);

		Assert.Contains(Permission.ManageUsers, permissions);
	}

	[Fact]
	public void A_member_holds_exactly_the_household_grants()
	{
		HashSet<Permission> permissions = PermissionResolution.Resolve(
			UserRole.Member,
			memberGrants:[Permission.CategorizeLines],
			overrides:null);

		Assert.Equal([Permission.CategorizeLines], permissions);
	}

	[Fact]
	public void A_member_with_no_grants_can_only_read()
	{
		Assert.Empty(PermissionResolution.Resolve(UserRole.Member, [], overrides:null));
	}

	/// <summary>
	/// Nothing writes overrides yet, but the resolution must already honour them, or enabling the column
	/// later would silently do nothing.
	/// </summary>
	[Fact]
	public void An_override_replaces_the_household_grants_rather_than_adding_to_them()
	{
		HashSet<Permission> permissions = PermissionResolution.Resolve(
			UserRole.Member,
			memberGrants:[Permission.CategorizeLines],
			overrides:[Permission.EditReceipts]);

		Assert.Equal([Permission.EditReceipts], permissions);
	}

	/// <summary>An empty override is a deliberate "nothing", distinct from having no override at all.</summary>
	[Fact]
	public void An_empty_override_revokes_the_household_grants()
	{
		Assert.Empty(PermissionResolution.Resolve(
			UserRole.Member,
			memberGrants:[Permission.CategorizeLines],
			overrides:[]));
	}

	[Theory]
	// targetIsActiveAdmin, otherActiveAdmins, newRole,        newStatus,            expected
	[InlineData(true,  0, UserRole.Member, UserStatus.Active,   true)]   // the last admin demoting itself
	[InlineData(true,  0, UserRole.Admin,  UserStatus.Disabled, true)]   // ...or disabling itself
	[InlineData(true,  0, UserRole.Admin,  UserStatus.Active,   false)]  // a no-op change is fine
	[InlineData(true,  1, UserRole.Member, UserStatus.Active,   false)]  // someone else still holds it
	[InlineData(false, 0, UserRole.Member, UserStatus.Disabled, false)]  // a member cannot be the last admin
	public void The_last_active_admin_cannot_be_removed(
		bool targetIsActiveAdmin, int otherActiveAdmins, UserRole newRole, UserStatus newStatus, bool expected)
	{
		Assert.Equal(
			expected,
			PermissionResolution.RemovesLastAdmin(targetIsActiveAdmin, otherActiveAdmins, newRole, newStatus));
	}
}
