namespace Backend.Features.Persistence.Model.Receipts;

public sealed class CanonicalItem
{
	public Guid Id { get; set; }
	public required string DisplayName { get; set; }
}