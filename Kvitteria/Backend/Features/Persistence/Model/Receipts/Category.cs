namespace Backend.Features.Persistence.Model.Receipts;

public sealed class Category
{
	public Guid Id { get; set; }
	public Guid? ParentCategoryId { get; set; }
	public required string DisplayName { get; set; }
	public int SortOrder { get; set; }

	public Category? ParentCategory { get; set; }
	public List<Category> ChildCategories { get; set; } = [];
}
