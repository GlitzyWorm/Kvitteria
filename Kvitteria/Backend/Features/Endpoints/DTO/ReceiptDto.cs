using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
namespace Backend.Features.Endpoints.DTO;

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
public static class ReceiptDtoExtension
{
	public static ReceiptDto ToDto(this Receipt receipt)
	{
		return new ReceiptDto(
			receipt.Id,
			receipt.Merchant!.Name,
			receipt.PurchaseDate,
			receipt.GrandTotal,
			receipt.PantTotal,
			receipt.Reconciled,
			receipt.Status,
			receipt.ReceiptLines.Select(rl => new ReceiptLineDto(
				rl.Id, rl.RawName, rl.Quantity, rl.UnitPrice, rl.LineTotal,
				rl.CanonicalItemId, rl.CanonicalItem?.DisplayName)).ToList()
		);
	}
}