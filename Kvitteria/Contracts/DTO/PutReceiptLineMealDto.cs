namespace Kvitteria.Contracts.DTO;

public record PutReceiptLineMealDto(
	Guid? MealId,
	bool SetAsDefault
);