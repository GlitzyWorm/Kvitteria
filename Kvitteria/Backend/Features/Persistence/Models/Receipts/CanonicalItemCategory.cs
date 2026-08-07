namespace Backend.Features.Persistence.Models.Receipts;

// Maps the relationships between each CanonicalItem and one or more Categories
public class CanonicalItemCategory
{
	public required Guid CanonicalItemId { get; set; }
	public required Guid CategoryId { get; set; }
	
	public CanonicalItem? CanonicalItem { get; set; }
	public Category? Category { get; set; }
}