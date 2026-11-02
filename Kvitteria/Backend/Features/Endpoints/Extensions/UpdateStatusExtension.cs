using Backend.Features.Update;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class UpdateStatusDtoExtension
{
	public static UpdateStatusDto ToDto(this UpdateRun? lastRun, string currentVersion, AvailableRelease? newest, DateTime? lastCheckedAt, bool canStart) =>
		new(
			currentVersion,
			newest?.Version,
			newest != null && ReleaseSelection.IsNewer(newest.Version, currentVersion),
			newest?.ReleaseUrl,
			lastCheckedAt,
			canStart,
			lastRun?.State,
			lastRun?.TargetVersion,
			lastRun?.Detail,
			lastRun?.UpdatedAt);
}
