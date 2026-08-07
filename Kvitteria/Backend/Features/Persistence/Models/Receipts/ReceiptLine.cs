namespace Backend.Features.Persistence.Models.Receipts;

public sealed class ReceiptLine
{
	public Guid Id { get; set; }
	public Guid ReceiptId { get; set; }
	public required string RawName { get; set; }
	public Guid? CanonicalItemId { get; set; }
	public short Quantity { get; set; }
	public decimal LineTotal { get; set; }
	
	public CanonicalItem? CanonicalItem { get; set; }
}