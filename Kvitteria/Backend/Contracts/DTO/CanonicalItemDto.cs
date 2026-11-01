namespace Kvitteria.Contracts.DTO;

public record CanonicalItemDto(
	Guid Id,
	string DisplayName,
	Guid? CategoryId,
	Guid? DefaultMealId
);