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
	List<ReceiptLineDto> ReceiptLines,
	List<ReceiptMealCoverageDto> ReceiptMealCoverage
);
public static class ReceiptDtoExtension
{
	/// <summary>
	/// Requires Merchant, ReceiptLines (with their CanonicalItem/Meal/Category) and MealCoverage.Meal
	/// to be included. Both receipt routes include all of them; the list route did not, which is why
	/// display names used to come back null there.
	/// </summary>
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
			receipt.ReceiptLines.Select(rl => rl.ToDto()).ToList(),
			receipt.MealCoverage.OrderBy(mc => mc.Meal!.SortOrder).Select(mc => mc.ToDto()).ToList()
		);
	}
}