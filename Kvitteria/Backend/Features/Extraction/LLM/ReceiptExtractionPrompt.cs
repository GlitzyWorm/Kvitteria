using System.Globalization;
using System.Text.Json;
namespace Backend.Features.Extraction.LLM;

public static class ReceiptExtractionPrompt
{
	private const string SchemaJson = """
		{
			"type": "object",
			"properties": {
				"merchant": { "type": "string" },
				"purchase_date": { "type": "string" },
				"currency": { "type": "string" },
				"grand_total": { "type": "number" },
				"pant_total": { "type": "number" },
				"line_items": {
					"type": "array",
					"items": {
						"type": "object",
						"properties": {
							"raw_name": { "type": "string" },
							"quantity": { "type": "number" },
							"unit": { "type": "string" },
							"unit_price": { "type": "number" },
							"line_total": { "type": "number" }
						},
						"required": ["raw_name", "quantity", "unit", "unit_price", "line_total"]
					}
				}
			},
			"required": ["merchant", "purchase_date", "currency", "grand_total", "pant_total", "line_items"]
		}
		""";

	public static readonly JsonElement Schema = ParseSchema();
	
	public const string SystemPrompt = """
		You extract structured data from Danish grocery receipts received as plain-text emails
		(Netto, Rema 1000, Føtex, Bilka, Lidl, Coop 365, Kvickly, SuperBrugsen, etc.).
		Return only data actually present on the receipt.

		Rules:
		- merchant: the store name as printed, without address, CVR number, or phone number. Also remove parts like "Scan&Go", "Scan og betal", or similar.
		- purchase_date: format YYYY-MM-DD HH:MM:SS. Danish receipts print dates like "4. juli 2026", "04-07-2026", or "Tidspunkt: 09-08-2026 11:17:40"; convert, keeping both date and time.
		- currency: ISO 4217 code. "kr", "kr." and "DKK" all mean DKK.
		- Danish numbers use a decimal comma: "12,95" means 12.95.
		- grand_total: the final amount paid ("Total", "I alt", "At betale"), after all discounts and neither the "HERAF PANT IALT" or "HERAF MOMS".
		- pant_total: the total bottle deposit ("HERAF PANT IALT"); 0 if the receipt has no such line.
		- The totals section may print the labels "HERAF PANT IALT", "HERAF MOMS", "I ALT" together,
		  followed by their three amounts in the same order. Then: first amount = pant_total,
		  second amount = VAT (do not extract it), third amount = grand_total. grand_total is always
		  the largest of the three. The grand_total is ALWAYS the last and largest number.
		- line_items: one entry per product on the receipt, in order. Include EVERY product exactly once.
		  The FIRST product comes immediately after the store header (address, "Momsnr.") — it is a
		  product, not part of the header. The LAST product comes immediately before the totals
		  section ("HERAF PANT IALT", "HERAF MOMS", "I ALT") — it is a product, not part of the totals.
		  Product names may contain abbreviations and percentages (e.g. "HK. OKSE 8-12%").
		  - Items are often printed as consecutive lines: product name, then a quantity line ("2 stk"),
		    then an amount. The printed amount is the TOTAL for the line (line_total), NOT the unit price.
		    This also holds when the quantity is 2 or more: line_total is the printed amount unchanged —
		    never multiply the printed amount by the quantity.
		  - raw_name: the product name exactly as printed, without the quantity.
		  - quantity: number of units; default 1.
		  - unit: the unit if printed ("stk", "kg", "g", "l", "ml"); otherwise "stk".
		  - unit_price: line_total divided by qty.
		  - Discount lines ("RABAT", "Tilbud", "Mængderabat") are separate line items with a negative line_total.
		  - Do NOT include summary lines (subtotal, "HERAF PANT IALT", "HERAF MOMS", "I ALT"), payment method,
		    card details, change ("Byttepenge"), or loyalty-point lines as line items.
		""";

	/// <summary>
	/// Asks a vision model to write out a photographed or screenshotted receipt as plain text, once. Its
	/// output is stored and extracted with <see cref="PhotoSystemPrompt"/>; the image is not read again.
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

	/// <summary>
	/// Extraction rules for a transcribed photo or screenshot. Kept apart from <see cref="SystemPrompt"/> so
	/// the mail receipts that reconcile today are untouched. Declarative and without example dates: the mail
	/// prompt's own example date leaked into photo extractions.
	/// </summary>
	public const string PhotoSystemPrompt = """
		You extract structured data from the text of a Danish shop receipt that was photographed or
		screenshotted from a store's app and then transcribed line by line
		(Netto, Rema 1000, Føtex, Bilka, Lidl, Coop 365, Kvickly, SuperBrugsen, etc.).
		Return only data actually present on the receipt.

		Rules:
		- merchant: the store name as printed, without address, CVR number, phone number or branch number.
		- purchase_date: format YYYY-MM-DD HH:MM:SS.
		  Danish receipts print the date day first, then month, then year, in one of these forms:
		  "DD MM YY", "DD MM YYYY", "DD.MM.YYYY" or "DD-MM-YYYY". A two-digit year means 20YY.
		  The day always comes before the month. The date and the time may be printed apart, on
		  different lines; combine them. Use 00:00:00 when no time is printed.
		  Take the date only from the receipt's text.
		- currency: ISO 4217 code. "kr", "kr." and "DKK" all mean DKK.
		- Danish numbers use a decimal comma: "12,95" means 12.95.
		- grand_total: the final amount paid, printed beside "TOTAL", "AT BETALE", "I ALT" or "Betalt", after all
		  discounts. It is never a VAT amount.
		- pant_total: the sum of the printed "PANT" lines (bottle deposit); 0 if there are none.
		  A "PANT" line is deposit, not a product: count it in pant_total and do not list it as a line item.
		- line_items: one entry per product on the receipt, in order. Include EVERY product exactly once,
		  including the first product printed after the store header and the last product before the totals.
		  - The amount printed on a product's line is the TOTAL for that line (line_total), not the unit price.
		    Never multiply it by the quantity.
		  - raw_name: the product name exactly as printed, without the quantity.
		  - quantity: number of units or the weight; default 1. A line such as "2 x 12,95" or "2 stk" gives the quantity.
		  - unit: the unit if printed ("stk", "kg", "g", "l", "ml"); otherwise "stk".
		  - unit_price: line_total divided by quantity.
		  - Discounts are separate line items with a negative line_total. A discount may be printed with the
		    minus after the amount ("RABAT 14,95-") or before it ("-50,00"); both mean a negative amount.
		- These are never line items: payment lines ("DANKORT", "BETALINGSKORT", "COOP-BETALING", "MobilePay",
		  card numbers), change ("BYTTEPENGE"), VAT lines ("MOMS", "MOMS UDGØR", "HERAF MOMS", "HERAF 25%"),
		  loyalty lines ("BONUSKØB", "BONUSKONTO", "Medlem"), subtotals, totals, lines whose only amount is 0,00,
		  and a summary line such as "Rabat på køb" or "Du har sparet" when the discounts are already printed
		  per product.
		""";

	/// <summary>Sent only when Ollama's content was not parseable receipt JSON.</summary>
	public const string MalformedJsonHint =
		"Your previous response could not be parsed as valid JSON matching the schema. "+
		"Respond with ONLY the JSON object, no extra commentary.";

	/// <summary>Sent when an extraction parsed cleanly but its totals did not reconcile.</summary>
	public static string ReconciliationHint(decimal claimedTotal, decimal grandTotal) =>
		string.Create(CultureInfo.InvariantCulture,
			$"Your previous extraction of this receipt was rejected: the line totals plus pant summed to {claimedTotal:0.00}, but the receipt's grand total is {grandTotal:0.00}. You probably missed a product, duplicated one, or got an amount wrong. Re-read the receipt and extract it again — especially check the FIRST product right after the store header and the LAST product right before the totals section, and check that no printed amount was multiplied by its quantity.");

	private static JsonElement ParseSchema()
	{
		using JsonDocument document = JsonDocument.Parse(SchemaJson);
		return document.RootElement.Clone();
	}
}
