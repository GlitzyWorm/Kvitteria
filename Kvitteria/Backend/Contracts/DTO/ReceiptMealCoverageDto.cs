namespace Kvitteria.Contracts.DTO;

public record ReceiptMealCoverageDto(
	Guid MealId,
	string MealDisplayName,
	int DaysCovered
);