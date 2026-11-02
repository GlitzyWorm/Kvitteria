using Backend.Features.Update;
namespace Backend.Tests;

public class ReleaseSelectionTests
{
	private static GitHubRelease Backend(string version, bool bundle = true, bool draft = false, bool prerelease = false) =>
		new(
			$"backend-v{version}",
			$"https://example.test/{version}",
			draft,
			prerelease,
			bundle ? [new GitHubAsset($"kvitteria-stack-{version}.tar.gz"), new GitHubAsset($"kvitteria-stack-{version}.sha256")] : []);

	[Fact]
	public void A_newer_release_of_another_component_is_ignored()
	{
		GitHubRelease mobile = new("mobile-v2.0.0", "https://example.test/mobile", false, false, [new GitHubAsset("kvitteria-stack-2.0.0.tar.gz")]);

		Assert.Equal("1.2.0", ReleaseSelection.Newest([mobile, Backend("1.2.0")])?.Version);
	}

	[Fact]
	public void A_release_without_its_bundle_is_not_available_yet()
	{
		Assert.Equal("1.2.0", ReleaseSelection.Newest([Backend("1.3.0", bundle:false), Backend("1.2.0")])?.Version);
	}

	[Fact]
	public void Another_versions_bundle_does_not_count()
	{
		GitHubRelease mismatched = new("backend-v1.3.0", "https://example.test/1.3.0", false, false, [new GitHubAsset("kvitteria-stack-1.2.0.tar.gz")]);

		Assert.Null(ReleaseSelection.Newest([mismatched]));
	}

	[Fact]
	public void Versions_compare_as_numbers()
	{
		Assert.Equal("1.10.0", ReleaseSelection.Newest([Backend("1.9.0"), Backend("1.10.0"), Backend("1.8.0")])?.Version);
	}

	[Fact]
	public void Drafts_and_prereleases_are_ignored()
	{
		GitHubRelease[] releases = [Backend("1.4.0", draft:true), Backend("1.3.0", prerelease:true), Backend("1.2.0")];

		Assert.Equal("1.2.0", ReleaseSelection.Newest(releases)?.Version);
	}

	[Fact]
	public void No_installable_release_means_none()
	{
		Assert.Null(ReleaseSelection.Newest([]));
		Assert.Null(ReleaseSelection.Newest([Backend("1.1.0", bundle:false)]));
	}

	[Fact]
	public void The_release_link_comes_with_the_version()
	{
		Assert.Equal("https://example.test/1.2.0", ReleaseSelection.Newest([Backend("1.2.0")])?.ReleaseUrl);
	}

	[Theory]
	[InlineData("1.2.0", "1.1.0", true)]
	[InlineData("1.10.0", "1.9.0", true)]
	[InlineData("1.1.0", "1.1.0", false)]
	[InlineData("1.0.0", "1.1.0", false)]
	[InlineData("1.2.0", "0.0.0-dev", false)] // a development build never has an update
	[InlineData("1.2.0", "unknown", false)]
	public void Only_a_higher_plain_version_is_newer(string candidate, string running, bool expected)
	{
		Assert.Equal(expected, ReleaseSelection.IsNewer(candidate, running));
	}
}
