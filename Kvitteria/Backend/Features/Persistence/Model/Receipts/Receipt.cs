using Backend.Features.Persistence.Model.Users;
using Kvitteria.Contracts.Shared;

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

	/// <summary>Why extraction was imperfect, when it was.</summary>
	public string? Error { get; set; }

	/// <summary>
	/// The member whose mailbox the mail arrived in. Null for receipts stored before attribution existed.
	/// Restrict on delete. Kept apart from <see cref="MailboxId"/> because a mailbox can be deleted
	/// while the receipt must keep the person it belonged to.
	/// </summary>
	public Guid? AppUserId { get; set; }
	public AppUser? AppUser { get; set; }

	/// <summary>The mailbox it came through. SetNull on delete.</summary>
	public Guid? MailboxId { get; set; }

	public List<ReceiptMealCoverage> MealCoverage { get; set; } = [];

	public Merchant? Merchant { get; set; }
	public List<ReceiptLine> ReceiptLines { get; set; } = [];
}
