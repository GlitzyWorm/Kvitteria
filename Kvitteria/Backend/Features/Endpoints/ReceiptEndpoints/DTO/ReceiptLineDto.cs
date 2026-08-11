namespace Backend.Features.Endpoints.ReceiptEndpoints.DTO;

public record ReceiptLineDto(
	Guid Id,
	string RawName,
	int Quantity,
	decimal UnitPrice,
	decimal LineTotal,
	Guid? CanonicalItemId,
	string? CanonicalItemDisplayName
);