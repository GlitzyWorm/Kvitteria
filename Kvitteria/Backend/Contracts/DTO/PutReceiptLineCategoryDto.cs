namespace Kvitteria.Contracts.DTO;

public record PutReceiptLineCategoryDto(
	Guid? CategoryId,
	bool SetAsDefault
);