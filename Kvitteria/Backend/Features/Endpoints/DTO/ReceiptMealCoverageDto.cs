using Backend.Features.Persistence.Model.Receipts;
namespace Backend.Features.Endpoints.DTO;

public record ReceiptMealCoverageDto(
	Guid MealId,
	string MealDisplayName,
	int DaysCovered
);

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