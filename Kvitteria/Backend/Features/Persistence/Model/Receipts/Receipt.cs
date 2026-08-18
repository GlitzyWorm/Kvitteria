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

	/// <summary>
	/// IMAP Message-Id of the mail this receipt came from. Uniquely indexed, so re-ingesting the same
	/// mail cannot create a duplicate receipt.
	/// </summary>
	public string? SourceMessageId { get; set; }

	/// <summary>
	/// Why extraction was imperfect, when it was. Previously this lived only on the in-memory pipeline
	/// record and was written in four places but never read, so unreconciled receipts carried no
	/// explanation once persisted.
	/// </summary>
	public string? Error { get; set; }

	public List<ReceiptMealCoverage> MealCoverage { get; set; } = [];

	public Merchant? Merchant { get; set; }
	public List<ReceiptLine> ReceiptLines { get; set; } = [];
}
