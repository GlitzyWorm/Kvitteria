using Backend.Features.Shared;
namespace Backend.Features.Persistence.Models.Receipts;

public sealed class Receipt
{
	public required Guid Id { get; set; }
	public Guid MerchantId { get; set; }
	public DateTime PurchaseDate { get; set; }
	public decimal GrandTotal { get; set; }
	public decimal PantTotal { get; set; }
	public bool Reconciled { get; set; }
	public ReceiptStatus Status { get; set; }

	public Merchant? Merchant { get; set; }
	public List<ReceiptLine> ReceiptLines { get; set; } = [];
}