using Backend.Features.Persistence.Model.Receipts;
namespace Backend.Features.Endpoints.DTO;

public record CanonicalItemDto(
	Guid Id,
	string DisplayName,
	Guid? CategoryId,
	Guid? DefaultMealId
);
public static class CanonicalItemDtoExtension
{
	public static CanonicalItemDto ToDto(this CanonicalItem canonicalItem)
	{
		return new CanonicalItemDto(
			canonicalItem.Id,
			canonicalItem.DisplayName,
			canonicalItem.CategoryId,
			canonicalItem.DefaultMealId
		);
	}
}