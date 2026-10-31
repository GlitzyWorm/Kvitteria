namespace Kvitteria.Contracts.DTO;

/// <summary>
/// One Danish calendar month. <paramref name="Total"/> is the sum of receipt grand totals and so includes
/// pant; <paramref name="LinesTotal"/> is the sum of line totals, which is what category shares divide by.
/// <paramref name="ChangePercent"/> is against the previous month, null when that month had no spending.
/// </summary>
public record SpendingSummaryDto(
	string Month,
	decimal Total,
	int ReceiptCount,
	decimal PantTotal,
	decimal LinesTotal,
	decimal UncategorizedTotal,
	decimal PreviousTotal,
	decimal? ChangePercent
);
