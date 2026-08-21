namespace Backend.Features.Auth;

/// <summary>
/// A write capability. Reads are never gated: the household sees everything, and the only thing worth
/// controlling is who may change what.
/// </summary>
public enum Permission
{
	/// <summary>Assign canonical item, category or meal to a line, and set a receipt's meal coverage.</summary>
	CategorizeLines,

	/// <summary>Create or change categories, meals, canonical items and aliases.</summary>
	ManageTaxonomy,

	/// <summary>Correct the extracted figures on a line — raw name, quantity, prices.</summary>
	EditReceipts,

	/// <summary>Pre-authorize, promote, demote and disable household members.</summary>
	ManageUsers,

	/// <summary>Change household-wide settings, including what members are allowed to do.</summary>
	ManageSettings,

	/// <summary>
	/// Add, edit and remove the IMAP mailboxes receipts are collected from. Separate from
	/// <see cref="ManageSettings"/> so a member can register their own mailbox without also being able
	/// to retune the pipeline or edit everyone's permissions.
	/// </summary>
	ManageMailboxes
}
