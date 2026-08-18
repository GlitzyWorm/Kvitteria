namespace Backend.Features.Endpoints.DTO;

public record PatchReceiptLineDto(
	string? RawName,
	decimal? Quantity,
	decimal? UnitPrice,
	decimal? LineTotal
);