using Backend.Features.Persistence.Model.Receipts;
namespace Backend.Features.Endpoints.DTO;

public record ItemAliasDto(Guid Id, string Alias, CanonicalItemDto CanonicalItem);

public static class ItemAliasDtoExtension
{
	public static ItemAliasDto ToDto(this ItemAlias itemAlias)
	{
		return new ItemAliasDto(
			itemAlias.Id,
			itemAlias.RawName,
			itemAlias.CanonicalItem?.ToDto()!
		);
	}
}