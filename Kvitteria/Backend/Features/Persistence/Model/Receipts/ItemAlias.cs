namespace Backend.Features.Persistence.Model.Receipts;

public sealed class ItemAlias
{
	public Guid Id { get; set; }
	public required string RawName { get; set; }
	public required Guid CanonicalItemId { get; set; }
	
	public CanonicalItem? CanonicalItem { get; set; }
}