using System.Reflection;

namespace Kvitteria.Contracts.DTO;

/// <summary>
/// What build is running. Deliberately just these two fields: the endpoint is unauthenticated, so it
/// volunteers nothing about the environment or the host.
/// </summary>
public record VersionDto(string Version, string Commit)
{
	private const string Unknown = "unknown";

	/// <summary>
	/// Read once from the assembly. The SDK appends <c>+{SourceRevisionId}</c> to the informational
	/// version, so one attribute carries both halves; the fallbacks keep a build without
	/// <c>Directory.Build.props</c> reporting a value instead of throwing.
	/// </summary>
	public static VersionDto Current { get; } = Read();

	private static VersionDto Read()
	{
		string? informational = Assembly.GetEntryAssembly()
		                               ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
		                               ?.InformationalVersion;

		if (string.IsNullOrWhiteSpace(informational))
			return new VersionDto(Unknown, Unknown);

		int separator = informational.IndexOf('+');

		return separator < 0
			? new VersionDto(informational, Unknown)
			: new VersionDto(informational[..separator], informational[(separator + 1)..]);
	}
}
