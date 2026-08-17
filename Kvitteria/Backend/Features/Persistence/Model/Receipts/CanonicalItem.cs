namespace Backend.Features.Persistence.Model.Receipts;

public sealed class CanonicalItem
{
	public Guid Id { get; set; }
	public required string DisplayName { get; set; }
	public Guid? CategoryId { get; set; }
	public Guid? DefaultMealId { get; set; }
	
	public Category? Category { get; set; }
	public Meal? DefaultMeal { get; set; }
}