namespace Backend.Features.Extraction;

public sealed record ExtractedReceipt(
	string Merchant,
	string PurchaseDate,
	string Currency,
	decimal GrandTotal,
	decimal PantTotal,
	IReadOnlyList<ExtractedLineItem> LineItems
);

public sealed record ExtractedLineItem(
	string RawName,
	decimal Quantity,
	string Unit,
	decimal UnitPrice,
	decimal LineTotal
);