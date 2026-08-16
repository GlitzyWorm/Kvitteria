namespace Backend.Features.Endpoints.DTO;

public record PatchReceiptLineDto(
	string? RawName,
	short? Quantity,
	decimal? UnitPrice,
	decimal? LineTotal
);