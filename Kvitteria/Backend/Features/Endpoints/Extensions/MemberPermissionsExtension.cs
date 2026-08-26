using Backend.Features.Auth;
using Backend.Features.Persistence.Model.Users;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

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
