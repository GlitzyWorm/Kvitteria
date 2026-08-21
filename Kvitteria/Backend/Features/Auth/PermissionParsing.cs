using Backend.Features.Shared;
namespace Backend.Features.Auth;

public static class PermissionParsing
{
	extension(IEnumerable<string> names)
	{
		/// <summary>
		/// Permissions cross the wire as their names rather than their ordinals, so a client is readable
		/// and reordering the enum cannot silently change what a member may do.
		/// </summary>
		public bool TryParsePermissions(out List<Permission> permissions, out string? invalidName)
		{
			permissions = [];

			foreach (string name in names)
			{
				if (!name.TryParseName(out Permission permission))
				{
					invalidName = name;
					return false;
				}

				if (!permissions.Contains(permission))
					permissions.Add(permission);
			}

			invalidName = null;
			return true;
		}
	}

	extension(IEnumerable<Permission> permissions)
	{
		public List<string> ToNames() => [.. permissions.Select(permission => permission.ToString())];
	}
}
