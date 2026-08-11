using Backend.Features.Shared;
namespace Backend.Features.Endpoints.ReceiptEndpoints.DTO;

public record ReceiptDto(
	Guid Id,
	string Merchant,
	DateTime PurchaseDate,
	decimal GrandTotal,
	decimal PantTotal,
	bool Reconciled,
	ReceiptStatus Status,
	List<ReceiptLineDto> ReceiptLines
);