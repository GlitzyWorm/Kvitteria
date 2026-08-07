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
							"qty": { "type": "number" },
							"unit": { "type": "string" },
							"unit_price": { "type": "number" },
							"line_total": { "type": "number" }
						},
						"required": ["raw_name", "qty", "unit", "unit_price", "line_total"]
					}
				}
			},
			"required": ["merchant", "purchase_date", "currency", "grand_total", "pant_total", "line_items"]
		}
		""";

	public static readonly JsonElement Schema = ParseSchema();
	
	// TODO: Grand total is always assigned MOMS value
	public const string SystemPrompt = """
		You extract structured data from Danish grocery receipts received as plain-text emails
		(Netto, Rema 1000, Føtex, Bilka, Lidl, Coop 365, Kvickly, SuperBrugsen, etc.).
		Return only data actually present on the receipt.

		Rules:
		- merchant: the store name as printed, without address, CVR number, or phone number.
		- purchase_date: format YYYY-MM-DD. Danish receipts print dates like "4. juli 2026" or "04-07-2026"; convert.
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
		  - qty: number of units; default 1.
		  - unit: the unit if printed ("stk", "kg", "g", "l", "ml"); otherwise "stk".
		  - unit_price: line_total divided by qty.
		  - Discount lines ("RABAT", "Tilbud", "Mængderabat") are separate line items with a negative line_total.
		  - Do NOT include summary lines (subtotal, "HERAF PANT IALT", "HERAF MOMS", "I ALT"), payment method,
		    card details, change ("Byttepenge"), or loyalty-point lines as line items.
		""";

	private static JsonElement ParseSchema()
	{
		using JsonDocument document = JsonDocument.Parse(SchemaJson);
		return document.RootElement.Clone();
	}
}
