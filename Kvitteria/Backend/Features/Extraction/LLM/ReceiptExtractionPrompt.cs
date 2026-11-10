using System.Globalization;
using System.Text.Json;
namespace Backend.Features.Extraction.LLM;

public static class ReceiptExtractionPrompt
{
	/// <summary>
	/// The amounts are strings held to a printed-amount pattern: as numbers the model cut <c>29,95</c> to 29
	/// (JSON has no decimal comma), and as free strings it copied quantity lines or left them empty. Ollama
	/// turns the pattern into a grammar. <c>rows</c> comes before <c>grand_total</c>, so every line is written
	/// before the total is stated.
	/// </summary>
	private const string SchemaJson = """
		{
			"type": "object",
			"properties": {
				"merchant": { "type": "string" },
				"currency": { "type": "string" },
				"rows": {
					"type": "array",
					"items": {
						"type": "object",
						"properties": {
							"text": { "type": "string" },
							"amount": { "type": "string", "pattern": "^-?[0-9]{1,3}(\\.?[0-9]{3})?,[0-9]{2}-?$" },
							"quantity": { "type": "number" },
							"kind": {
								"type": "string",
								"enum": ["product", "discount", "deposit", "deposit_total", "total", "payment", "vat", "loyalty", "other"]
							}
						},
						"required": ["text", "amount", "quantity", "kind"]
					}
				},
				"grand_total": { "type": "string", "pattern": "^-?[0-9]{1,3}(\\.?[0-9]{3})?,[0-9]{2}-?$" }
			},
			"required": ["merchant", "currency", "rows", "grand_total"]
		}
		""";

	public static readonly JsonElement Schema = ParseSchema();
	
	/// <summary>
	/// The one extraction prompt, for a mail's text and for an image's transcription alike. The model only
	/// labels what is printed; <see cref="ReceiptRows"/> decides what counts, and the date and the chain are
	/// read by code. Declarative and without example dates, which leaked into earlier extractions.
	/// </summary>
	public const string ExtractionPrompt = """
		You read the text of a Danish shop receipt (Netto, Rema 1000, Føtex, Bilka, Lidl, Coop 365, Kvickly,
		SuperBrugsen, etc.), received as a mail or transcribed from a photo, and list every printed line that
		has an amount. You only label and copy; you never calculate.

		Rules:
		- merchant: the shop name as printed, without address, CVR number, phone number, branch number or words
		  such as "Scan&Go".
		- currency: ISO 4217 code. "kr", "kr." and "DKK" all mean DKK.
		- rows: EVERY printed line that carries an amount, once, in printed order, including the first line
		  after the store header and the last line before the totals. For each row:
		  - text: the line's text as printed, without its amount.
		  - amount: the amount exactly as printed, with its decimal comma and any minus sign before or after it
		    (for example "29,95", "14,95-", "-50,00", "1.234,50"). Never write a quantity, a calculation or
		    an empty value here.
		  - quantity: the number of units or the weight when the line prints it ("2 x 12,00", "2 stk"),
		    otherwise 1. A quantity line belongs to the product above it and is never a row of its own.
		    The amount of a product is its line total, not the unit price; never multiply it by the quantity.
		    A size in a name ("24X33 CL") is not a quantity.
		  - kind, exactly one of:
		    product: something bought.
		    discount: a reduction on the receipt's lines ("RABAT", "Tilbud", "Mængderabat"), whatever the sign
		      it is printed with.
		    deposit: a bottle deposit line ("PANT").
		    deposit_total: a deposit summary ("HERAF PANT IALT").
		    total: the amount to pay or the receipt's sum ("TOTAL", "I ALT", "AT BETALE", "Subtotal").
		    payment: how it was paid, change and card lines ("DANKORT", "BETALINGSKORT", "MobilePay",
		      "BYTTEPENGE").
		    vat: a VAT line ("MOMS", "HERAF MOMS", "MOMS UDGØR").
		    loyalty: bonus and membership lines ("BONUSKØB", "COOP BONUSKONTO", "Medlem").
		    other: anything else with an amount, such as a savings summary ("Du har sparet", "Rabat på køb").
		- Some mails print the labels "HERAF PANT IALT", "HERAF MOMS" and "I ALT" first, followed by their
		  three amounts in the same order. Give each label its own row with the amount in the same position.
		- grand_total: the amount actually paid, as printed, after all discounts. It is never a VAT amount.
		""";

	/// <summary>
	/// Asks a vision model to write out a photographed or screenshotted receipt as plain text, once. Its
	/// output is stored and extracted with <see cref="ExtractionPrompt"/>; the image is not read again.
	/// </summary>
	public const string TranscriptionPrompt = """
		You transcribe photographs and screenshots of Danish shop receipts into plain text.

		Rules:
		- Write out every printed line of the receipt from top to bottom, exactly as printed, one line per line.
		- Keep each label on the same line as the amount printed beside it.
		- Copy numbers exactly as printed, with their decimal commas and any minus sign before or after the amount (for example "14,95-").
		- Copy dates and times exactly as printed, with their spaces, dots or dashes. Do not reformat them.
		- Do not translate, summarize, correct, explain or add anything. Write only the receipt's text.
		- Leave out anything that is not part of the receipt, such as the app's buttons, menus or the phone's status bar.
		- If the image does not show a shop receipt, answer exactly NO_RECEIPT and nothing else.
		""";

	/// <summary>Sent only when Ollama's content was not parseable receipt JSON.</summary>
	public const string MalformedJsonHint =
		"Your previous response could not be parsed as valid JSON matching the schema. "+
		"Respond with ONLY the JSON object, no extra commentary.";

	/// <summary>Sent when an extraction parsed cleanly but its totals did not reconcile.</summary>
	public static string ReconciliationHint(decimal claimedTotal, decimal grandTotal) =>
		string.Create(CultureInfo.InvariantCulture,
			$"Your previous extraction of this receipt was rejected: the products and discounts plus pant summed to {claimedTotal:0.00}, but the receipt's grand total is {grandTotal:0.00}. You probably missed a line, listed one twice, or copied an amount wrongly. Read the receipt again and list every line that has an amount exactly once, in printed order, and copy each amount as printed.");

	private static JsonElement ParseSchema()
	{
		using JsonDocument document = JsonDocument.Parse(SchemaJson);
		return document.RootElement.Clone();
	}
}
