using Backend.Features.Persistence.Model.Receipts;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

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