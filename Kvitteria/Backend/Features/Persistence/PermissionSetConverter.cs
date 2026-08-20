using Backend.Features.Auth;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace Backend.Features.Persistence;

/// <summary>
/// Stores a permission set as a Postgres <c>text[]</c> of enum names, for the same reason every other
/// enum here is stored as text: the value stays readable in the database and survives reordering the
/// enum. A comparer is required because EF cannot detect mutation of a converted collection otherwise.
/// </summary>
internal sealed class PermissionSetConverter : ValueConverter<List<Permission>, string[]>
{
	public PermissionSetConverter() : base(
		permissions => permissions.Select(permission => permission.ToString()).ToArray(),
		names => names.Select(Enum.Parse<Permission>).ToList())
	{ }
}

internal sealed class PermissionSetComparer : ValueComparer<List<Permission>>
{
	public PermissionSetComparer() : base(
		(left, right) => left != null && right != null && left.SequenceEqual(right),
		permissions => permissions.Aggregate(0, (hash, permission) => HashCode.Combine(hash, permission)),
		permissions => permissions.ToList())
	{ }
}
