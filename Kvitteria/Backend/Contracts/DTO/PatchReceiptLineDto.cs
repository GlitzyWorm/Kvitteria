namespace Kvitteria.Contracts.DTO;

public record PatchReceiptLineDto(
	string? RawName,
	decimal? Quantity,
	decimal? UnitPrice,
	decimal? LineTotal
);