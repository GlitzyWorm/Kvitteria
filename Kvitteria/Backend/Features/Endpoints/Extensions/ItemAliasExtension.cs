using Backend.Features.Persistence.Model.Receipts;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class ItemAliasDtoExtension
{
	public static ItemAliasDto ToDto(this ItemAlias itemAlias)
	{
		return new ItemAliasDto(
			itemAlias.Id,
			itemAlias.RawName,
			itemAlias.CanonicalItem?.ToDto()
		);
	}
}