namespace Backend.Features.Endpoints.DTO;

public record PutReceiptLineMealDto(
	Guid? MealId,
	bool SetAsDefault
);