using Backend.Features.Persistence.Model.Receipts;
namespace Backend.Features.Endpoints.DTO;

public record ReceiptLineDto(
	Guid Id,
	string RawName,
	decimal Quantity,
	decimal UnitPrice,
	decimal LineTotal,
	Guid? CanonicalItemId,
	string? CanonicalItemDisplayName,
	Guid? MealId,
	string? MealDisplayName,
	Guid? CategoryId,
	string? CategoryDisplayName
);

public static class ReceiptLineDtoExtension
{
	public static ReceiptLineDto ToDto(this ReceiptLine receiptLine)
	{
		return new ReceiptLineDto(
			receiptLine.Id,
			receiptLine.RawName,
			receiptLine.Quantity,
			receiptLine.UnitPrice,
			receiptLine.LineTotal,
			receiptLine.CanonicalItemId,
			receiptLine.CanonicalItem?.DisplayName,
			receiptLine.MealId,
			receiptLine.Meal?.DisplayName,
			receiptLine.CategoryId,
			receiptLine.Category?.DisplayName
		);
	}
}