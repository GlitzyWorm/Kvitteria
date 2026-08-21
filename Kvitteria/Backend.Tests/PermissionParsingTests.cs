using Backend.Features.Auth;
namespace Backend.Tests;

public class PermissionParsingTests
{
	[Fact]
	public void Every_permission_survives_a_round_trip_through_its_name()
	{
		List<string> names = Enum.GetValues<Permission>().ToNames();

		Assert.True(names.TryParsePermissions(out List<Permission> parsed, out string? invalid));
		Assert.Null(invalid);
		Assert.Equal(Enum.GetValues<Permission>(), parsed);
	}

	[Fact]
	public void An_unknown_name_is_reported_rather_than_ignored()
	{
		List<string> names = ["CategorizeLines", "DeleteEverything"];

		Assert.False(names.TryParsePermissions(out _, out string? invalid));
		Assert.Equal("DeleteEverything", invalid);
	}

	/// <summary>
	/// Ordinals would be a wire contract nobody can read and that reordering the enum would silently
	/// change, so a number is not a permission.
	/// </summary>
	[Fact]
	public void A_number_is_not_a_permission_name()
	{
		Assert.False(new[] { "0" }.TryParsePermissions(out _, out string? invalid));
		Assert.Equal("0", invalid);
	}

	[Fact]
	public void Casing_is_forgiven()
	{
		Assert.True(new[] { "categorizelines" }.TryParsePermissions(out List<Permission> parsed, out _));
		Assert.Equal([Permission.CategorizeLines], parsed);
	}

	[Fact]
	public void A_repeated_name_is_not_granted_twice()
	{
		Assert.True(new[] { "CategorizeLines", "CategorizeLines" }.TryParsePermissions(out List<Permission> parsed, out _));
		Assert.Equal([Permission.CategorizeLines], parsed);
	}

	[Fact]
	public void An_empty_list_grants_nothing_and_is_not_an_error()
	{
		Assert.True(Array.Empty<string>().TryParsePermissions(out List<Permission> parsed, out _));
		Assert.Empty(parsed);
	}
}
