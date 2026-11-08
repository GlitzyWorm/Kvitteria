namespace Backend.Features.Extraction;

/// <summary>What the transcription step's answer means before anything is extracted from it.</summary>
public static class ReceiptTranscription
{
	/// <summary>The exact answer the transcription prompt asks for when the image shows no receipt.</summary>
	public const string NoReceiptSentinel = "NO_RECEIPT";

	/// <summary>The fixed failure recorded for such an image, in place of anything the model wrote.</summary>
	public const string NoReceiptError = "No receipt was found in the image.";

	/// <summary>
	/// Whether the model answered with the sentinel alone. Case, surrounding whitespace and a trailing full
	/// stop are tolerated; a transcription that merely contains the word is a transcription.
	/// </summary>
	public static bool IsNoReceipt(string? text)
	{
		if (text is null)
			return false;

		string trimmed = text.Trim();

		if (trimmed.EndsWith('.'))
			trimmed = trimmed[..^1].TrimEnd();

		return string.Equals(trimmed, NoReceiptSentinel, StringComparison.OrdinalIgnoreCase);
	}
}
