using Kvitteria.Contracts.Shared;

namespace Kvitteria.Contracts.DTO;

public record ReceiptDto(
	Guid Id,
	string Merchant,
	DateTime PurchaseDate,
	decimal GrandTotal,
	decimal PantTotal,
	/// <summary>Reconciled, Accepted or Unreconciled, by name.</summary>
	string Reconciliation,
	/// <summary>Set only while Reconciliation is Accepted.</summary>
	ReceiptAcceptanceDto? Acceptance,
	ReceiptStatus Status,
	List<ReceiptLineDto> ReceiptLines,
	List<ReceiptMealCoverageDto> ReceiptMealCoverage,
	string? Error,
	Guid? MemberId,
	string? MemberDisplayName,
	bool HasImage,
	Guid? PossibleDuplicateOfId
);