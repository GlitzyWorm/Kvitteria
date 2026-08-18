using Backend.Features.Shared;
namespace Backend.Features.Persistence.Model.Receipts;

public sealed class Receipt
{
	public Guid Id { get; set; }
	public Guid MerchantId { get; set; }
	public DateTime PurchaseDate { get; set; }
	public decimal GrandTotal { get; set; }
	public decimal PantTotal { get; set; }
	public bool Reconciled { get; set; }
	public ReceiptStatus Status { get; set; }
	public List<ReceiptMealCoverage> MealCoverage { get; set; } = [];

	public Merchant? Merchant { get; set; }
	public List<ReceiptLine> ReceiptLines { get; set; } = [];
}