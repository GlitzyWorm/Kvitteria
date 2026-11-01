namespace Kvitteria.Contracts.DTO;

public record CreateCategoryDto(
	string DisplayName,
	Guid? ParentCategoryId
);