namespace Backend.Features.Insights;

// Narrow projections the insight handlers load, so the rollups stay pure functions over plain values.

public sealed record ReceiptFigure(DateTime PurchaseDate, decimal GrandTotal, decimal PantTotal);

public sealed record LineFigure(DateTime PurchaseDate, decimal LineTotal, Guid? CategoryId);
