using Backend.Features.Persistence.Model.Receipts;
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

public static class ReceiptLineExtensions
{
	public static ReceiptLineDto ToDto(this ReceiptLine receiptLine, CanonicalItem? canonicalItem)
	{
		return new ReceiptLineDto(
			receiptLine.Id,
			receiptLine.RawName,
			receiptLine.Quantity,
			receiptLine.UnitPrice,
			receiptLine.LineTotal,
			receiptLine.CanonicalItemId,
			canonicalItem?.DisplayName
		);
	}
}