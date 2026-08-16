using Backend.Features.Persistence.Model.Receipts;
namespace Backend.Features.Endpoints.DTO;

public record CanonicalItemDto(Guid Id, string DisplayName);

public static class CanonicalItemDtoExtension
{
	public static CanonicalItemDto ToDto(this CanonicalItem canonicalItem)
	{
		return new CanonicalItemDto(
			canonicalItem.Id,
			canonicalItem.DisplayName
		);
	}
}