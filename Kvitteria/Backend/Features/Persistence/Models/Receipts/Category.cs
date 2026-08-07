namespace Backend.Features.Persistence.Models.Receipts;

public class Category
{
	public required Guid Id { get; set; }
	public Guid? ParentCategoryId { get; set; }
	public required string DisplayName { get; set; }
	
	public Category? ParentCategory { get; set; }
}