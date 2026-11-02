using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
namespace Backend.Features.Update;

public sealed record GitHubAsset([property: JsonPropertyName("name")] string Name);

public sealed record GitHubRelease(
	[property: JsonPropertyName("tag_name")]
	string TagName,
	[property: JsonPropertyName("html_url")]
	string HtmlUrl,
	[property: JsonPropertyName("draft")] bool Draft,
	[property: JsonPropertyName("prerelease")]
	bool Prerelease,
	[property: JsonPropertyName("assets")] List<GitHubAsset> Assets
);

public sealed record AvailableRelease(string Version, string ReleaseUrl);

/// <summary>
/// Which release counts as the newest. <c>scripts/update.sh</c> answers the same question with
/// <c>grep</c>, because it has to work with no backend running.
/// </summary>
public static partial class ReleaseSelection
{
	private const string TagPrefix = "backend-v";

	/// <summary>
	/// The highest backend release carrying a bundle. The repository also holds other components' releases,
	/// and a backend release gets its bundle only once its image is pushed, so "has a bundle" is what
	/// "can be installed" means.
	/// </summary>
	public static AvailableRelease? Newest(IEnumerable<GitHubRelease> releases)
	{
		AvailableRelease? newest = null;

		foreach (GitHubRelease release in releases)
		{
			if (release.Draft || release.Prerelease || !release.TagName.StartsWith(TagPrefix, StringComparison.Ordinal))
				continue;

			string version = release.TagName[TagPrefix.Length..];

			if (!IsPlainVersion(version) || release.Assets.All(asset => asset.Name != $"kvitteria-stack-{version}.tar.gz"))
				continue;

			if (newest == null || Version.Parse(version) > Version.Parse(newest.Version))
				newest = new AvailableRelease(version, release.HtmlUrl);
		}

		return newest;
	}

	/// <summary>False for anything but a plain release version, so <c>0.0.0-dev</c> never has an update.</summary>
	public static bool IsNewer(string candidate, string running) =>
		IsPlainVersion(candidate) && IsPlainVersion(running) && Version.Parse(candidate) > Version.Parse(running);

	private static bool IsPlainVersion(string version) => PlainVersion().IsMatch(version);

	[GeneratedRegex(@"^\d+\.\d+\.\d+$")]
	private static partial Regex PlainVersion();
}
