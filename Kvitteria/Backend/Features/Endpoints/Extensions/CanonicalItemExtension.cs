using Backend.Features.Persistence.Model.Receipts;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

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