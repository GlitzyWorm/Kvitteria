using Backend.Features.Persistence.Model.Receipts;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;
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