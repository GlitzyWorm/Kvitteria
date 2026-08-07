namespace Backend.Features.Persistence.Models.Receipts;

public sealed class CanonicalItem
{
	public required	Guid Id { get; set; }
	public required string DisplayName { get; set; }
}