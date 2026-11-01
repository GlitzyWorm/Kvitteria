namespace Kvitteria.Contracts.DTO;

public record ReceiptLineDto(
	Guid Id,
	string RawName,
	decimal Quantity,
	decimal UnitPrice,
	decimal LineTotal,
	Guid? CanonicalItemId,
	string? CanonicalItemDisplayName,
	Guid? MealId,
	string? MealDisplayName,
	Guid? CategoryId,
	string? CategoryDisplayName
);