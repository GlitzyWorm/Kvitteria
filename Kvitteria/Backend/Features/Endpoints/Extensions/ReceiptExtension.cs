using Backend.Features.Persistence.Model.Receipts;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class ReceiptDtoExtension
{
	/// <summary>
	/// Requires Merchant, AppUser, ReceiptLines (with their CanonicalItem/Meal/Category) and MealCoverage.Meal
	/// to be included; display names are null otherwise.
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
			receipt.MealCoverage.OrderBy(mc => mc.Meal!.SortOrder).Select(mc => mc.ToDto()).ToList(),
			receipt.Error,
			receipt.AppUserId,
			receipt.AppUser?.DisplayName
		);
	}
}