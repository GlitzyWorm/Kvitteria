namespace Kvitteria.Contracts.DTO;

/// <summary>One month of the trend. <paramref name="ChangePercent"/> is against the month before it.</summary>
public record SpendingTrendMonthDto(
	string Month,
	decimal Total,
	int ReceiptCount,
	decimal? ChangePercent
);
