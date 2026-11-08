using Backend.Features.Extraction;
namespace Backend.Tests;

public class ReceiptTranscriptionTests
{
	[Theory]
	[InlineData("NO_RECEIPT")]
	[InlineData("no_receipt")]
	[InlineData("  NO_RECEIPT \n")]
	[InlineData("NO_RECEIPT.")]
	[InlineData(" No_Receipt . ")]
	public void Recognises_the_sentinel(string text) =>
		Assert.True(ReceiptTranscription.IsNoReceipt(text));

	[Theory]
	[InlineData("NETTO\nMÆLK 12,95\nNO_RECEIPT\nI ALT 12,95")]
	[InlineData("NO_RECEIPT found")]
	[InlineData("")]
	[InlineData(null)]
	public void Treats_anything_else_as_a_transcription(string? text) =>
		Assert.False(ReceiptTranscription.IsNoReceipt(text));
}
