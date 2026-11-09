using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Receipts;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class ReceiptDtoExtension
{
	/// <summary>
	/// Requires Merchant, AppUser, AcceptedBy, ReceiptLines (with their CanonicalItem/Meal/Category) and MealCoverage.Meal
	/// to be included; display names are null otherwise.
	/// </summary>
	public static ReceiptDto ToDto(this Receipt receipt, decimal tolerance)
	{
		ReconciliationState state = ReceiptReconciliation.Classify(ReceiptReconciliation.Delta(receipt), tolerance, receipt.AcceptedDifference);

		// A lapsed acceptance stays in the row and is never sent.
		ReceiptAcceptanceDto? acceptance = state == ReconciliationState.Accepted
			? new ReceiptAcceptanceDto(
				receipt.AcceptedDifference!.Value,
				receipt.AcceptedById,
				receipt.AcceptedBy?.DisplayName,
				DateTime.SpecifyKind(receipt.AcceptedAt!.Value, DateTimeKind.Utc),
				receipt.AcceptedNote)
			: null;

		return new ReceiptDto(
			receipt.Id,
			receipt.Merchant!.Name,
			receipt.PurchaseDate,
			receipt.GrandTotal,
			receipt.PantTotal,
			state.ToString(),
			acceptance,
			receipt.Status,
			receipt.ReceiptLines.Select(rl => rl.ToDto()).ToList(),
			receipt.MealCoverage.OrderBy(mc => mc.Meal!.SortOrder).Select(mc => mc.ToDto()).ToList(),
			receipt.Error,
			receipt.AppUserId,
			receipt.AppUser?.DisplayName,
			// The key alone: the image is fetched separately and never loaded with a receipt.
			receipt.ImageId != null,
			receipt.PossibleDuplicateOfId
		);
	}
}