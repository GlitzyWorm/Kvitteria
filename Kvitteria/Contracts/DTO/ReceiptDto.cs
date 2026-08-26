using Kvitteria.Contracts.Shared;

namespace Kvitteria.Contracts.DTO;

public record ReceiptDto(
	Guid Id,
	string Merchant,
	DateTime PurchaseDate,
	decimal GrandTotal,
	decimal PantTotal,
	bool Reconciled,
	ReceiptStatus Status,
	List<ReceiptLineDto> ReceiptLines,
	List<ReceiptMealCoverageDto> ReceiptMealCoverage
);