namespace Kvitteria.Contracts.DTO;

public record CreateReceiptLineDto(
	string RawName,
	decimal Quantity,
	decimal UnitPrice,
	decimal LineTotal
);
