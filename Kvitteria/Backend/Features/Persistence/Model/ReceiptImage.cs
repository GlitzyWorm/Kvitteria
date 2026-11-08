namespace Backend.Features.Persistence.Model;

/// <summary>
/// An uploaded receipt image. Kept in its own table so the blob never rides along with a receipt or
/// intake query, and in the database rather than on a volume so one <c>pg_dump</c> stays a complete
/// backup. Linked from the intake while it is unresolved and from the receipt it became afterwards.
/// </summary>
public sealed class ReceiptImage
{
	public Guid Id { get; set; }

	/// <summary>The JPEG exactly as uploaded. The server checks it but never re-encodes it.</summary>
	public byte[] Content { get; set; } = [];

	/// <summary>SHA-256 of <see cref="Content"/>, uniquely indexed, so identical bytes are processed once.</summary>
	public byte[] Sha256 { get; set; } = [];

	public int Width { get; set; }
	public int Height { get; set; }
	public DateTime CreatedAt { get; set; }
}
