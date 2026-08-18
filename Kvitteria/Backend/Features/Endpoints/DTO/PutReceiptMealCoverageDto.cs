namespace Backend.Features.Endpoints.DTO;

public record PutReceiptMealCoverageDto(
	List<MealCoverageEntryDto> Coverage
);

public record MealCoverageEntryDto(
	Guid MealId,
	int DaysCovered
);