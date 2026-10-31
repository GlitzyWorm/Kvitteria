namespace Kvitteria.Contracts.DTO;

/// <summary>
/// A month's line totals by category. Without <paramref name="ParentCategoryId"/> the rows are top-level
/// categories and <paramref name="Uncategorized"/> holds the lines with no category; for a drill-down the
/// rows are that category's children, with the parent itself as a row when lines sit directly on it, and
/// <paramref name="Uncategorized"/> is null. <paramref name="CompareToMonth"/> is null when no comparison
/// was asked for, and every comparison figure is then null too.
/// </summary>
public record CategoryBreakdownDto(
	string Month,
	string? CompareToMonth,
	Guid? ParentCategoryId,
	List<CategoryAmountDto> Rows,
	CategoryAmountDto? Uncategorized
);

/// <summary>
/// One row of a breakdown. <paramref name="CategoryId"/> and <paramref name="DisplayName"/> are null on the
/// uncategorized row. <paramref name="ChangePercent"/> is null when the comparison amount is zero.
/// </summary>
public record CategoryAmountDto(
	Guid? CategoryId,
	string? DisplayName,
	decimal Amount,
	decimal? CompareAmount,
	decimal? ChangePercent
);
