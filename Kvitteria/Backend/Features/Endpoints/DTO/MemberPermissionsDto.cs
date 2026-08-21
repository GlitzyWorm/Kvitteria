using Backend.Features.Auth;
using Backend.Features.Persistence.Model.Users;
namespace Backend.Features.Endpoints.DTO;

/// <summary>What every member may do. Admins are unaffected and always hold everything.</summary>
public record MemberPermissionsDto(List<string> Permissions, List<string> AvailablePermissions);

public record PutMemberPermissionsDto(List<string> Permissions);

public static class MemberPermissionsDtoExtension
{
	public static MemberPermissionsDto ToDto(this HouseholdSettings settings)
	{
		return new MemberPermissionsDto(
			settings.MemberPermissions.ToNames(),
			Enum.GetValues<Permission>().ToNames()
		);
	}
}
