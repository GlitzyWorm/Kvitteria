using Backend.Features.Persistence.Model.Receipts;
namespace Backend.Features.Endpoints.DTO;

public record CategoryDto(
	Guid Id,
	string DisplayName,
	Guid? ParentCategoryId,
	List<CategoryDto> ChildCategories,
	int SortOrder
);

public static class CategoryDtoExtension
{
	public static CategoryDto ToDto(this Category category)
	{
		return new CategoryDto(
			category.Id,
			category.DisplayName,
			category.ParentCategoryId,
			category.ChildCategories.OrderBy(child => child.SortOrder).Select(child => child.ToDto()).ToList(),
			category.SortOrder
		);
	}
}