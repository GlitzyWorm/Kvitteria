namespace Kvitteria.Contracts.DTO;

public record MealDto(
	Guid Id,
	string DisplayName,
	int SortOrder,
	bool TracksDayCoverage
);