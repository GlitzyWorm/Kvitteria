namespace Kvitteria.Contracts.DTO;

/// <summary>The saved line, how many other lines the save assigned, and how many lines still wait.</summary>
public record SortReceiptLineResultDto(
	ReceiptLineDto Line,
	int OtherLinesAssigned,
	int RemainingCount
);
