namespace Backend.Features.Endpoints.ReceiptEndpoints.DTO;

public record PatchReceiptLineDto(
	string? RawName,
	short? Quantity,
	decimal? UnitPrice,
	decimal? LineTotal
);