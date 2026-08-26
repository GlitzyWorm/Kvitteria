using Backend.Features.Persistence.Model.Receipts;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class ReceiptMealCoverageDtoExtension
{
	public static ReceiptMealCoverageDto ToDto(this ReceiptMealCoverage receiptMealCoverage)
	{
		return new ReceiptMealCoverageDto(
			receiptMealCoverage.MealId,
			receiptMealCoverage.Meal!.DisplayName,
			receiptMealCoverage.DaysCovered
		);
	}
}