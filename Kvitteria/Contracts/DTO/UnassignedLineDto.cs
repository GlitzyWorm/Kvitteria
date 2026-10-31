namespace Kvitteria.Contracts.DTO;

/// <summary>A receipt line still waiting for a canonical item, with the receipt context the Sort screen shows.</summary>
public record UnassignedLineDto(
	Guid Id,
	string RawName,
	decimal Quantity,
	decimal UnitPrice,
	decimal LineTotal,
	Guid ReceiptId,
	string MerchantName,
	DateTime PurchaseDate,
	Guid? MemberId,
	string? MemberDisplayName
);
