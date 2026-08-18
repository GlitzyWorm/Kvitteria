namespace Backend.Features.Persistence.Model.Receipts;

public sealed class ReceiptMealCoverage
{
	public Guid ReceiptId { get; set; }
	public Guid MealId { get; set; }
	public int DaysCovered { get; set; }
	
	public Receipt? Receipt { get; set; }
	public Meal? Meal { get; set; }
}