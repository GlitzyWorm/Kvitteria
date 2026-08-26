namespace Kvitteria.Contracts.DTO;

public record CategoryDto(
	Guid Id,
	string DisplayName,
	Guid? ParentCategoryId,
	List<CategoryDto> ChildCategories,
	int SortOrder
);