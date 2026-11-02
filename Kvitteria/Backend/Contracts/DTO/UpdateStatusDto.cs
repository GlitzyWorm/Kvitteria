namespace Kvitteria.Contracts.DTO;

/// <summary>
/// Where this server stands against the newest release, and how its last update went.
/// <see cref="State"/> is <c>running</c>, <c>succeeded</c>, <c>failed</c>, <c>rolled-back</c>,
/// <c>restore-required</c> or <c>interrupted</c>, and null before any update has run.
/// </summary>
public record UpdateStatusDto(
	string CurrentVersion,
	string? LatestVersion,
	bool UpdateAvailable,
	string? ReleaseUrl,
	DateTime? LastCheckedAt,
	bool CanStart,
	string? State,
	string? TargetVersion,
	string? Detail,
	DateTime? UpdatedAt
);
