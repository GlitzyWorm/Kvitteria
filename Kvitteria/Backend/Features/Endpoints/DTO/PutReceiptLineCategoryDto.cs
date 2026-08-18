namespace Backend.Features.Endpoints.DTO;

public record PutReceiptLineCategoryDto(
	Guid? CategoryId,
	bool SetAsDefault
);