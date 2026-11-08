namespace Backend.Features.Persistence.Model;

/// <summary>Where an intake came from. Stored as its name, like every other status column.</summary>
public enum IntakeSource
{
	/// <summary>A receipt mail fetched over IMAP.</summary>
	Mail,

	/// <summary>A receipt image uploaded by a member.</summary>
	Upload
}
