namespace Backend.Features.Persistence.Model.Receipts;

public sealed class ReceiptLine
{
	public Guid Id { get; set; }
	public Guid ReceiptId { get; set; }
	public required string RawName { get; set; }
	public Guid? CanonicalItemId { get; set; }
	public short Quantity { get; set; }
	public decimal UnitPrice { get; set; }
	public decimal LineTotal { get; set; }
	public Guid? CategoryId { get; set; }
	public Guid? MealId { get; set; }
	
	public CanonicalItem? CanonicalItem { get; set; }
	public Receipt? Receipt { get; set; }
	public Category? Category { get; set; }
	public Meal? Meal { get; set; }
}