namespace Kvitteria.Contracts.DTO;

public record UpdateCanonicalItemDto(
	string? DisplayName,
	Guid? CategoryId,
	Guid? DefaultMealId,
	bool ClearCategory = false,
	bool ClearDefaultMeal = false
);