namespace Kvitteria.Contracts.DTO;

public record PatchReceiptDto(
	decimal? GrandTotal,
	decimal? PantTotal
);
