namespace Kvitteria.Contracts.DTO;

/// <summary>
/// A canonical item's price per unit month by month, oldest first. <paramref name="LatestPrice"/> and
/// <paramref name="LowestPrice"/> cover the run; <paramref name="ChangePercent"/> goes from the first priced
/// month to the latest and is null unless two months are priced. <paramref name="RawNameKey"/> and
/// <paramref name="RawNameLabel"/> are set only when the history was narrowed to one raw name.
/// </summary>
public record PriceHistoryDto(
	Guid ItemId,
	string DisplayName,
	List<PriceMonthDto> Months,
	decimal? LatestPrice,
	decimal? LowestPrice,
	decimal? ChangePercent,
	string? RawNameKey,
	string? RawNameLabel
);

/// <summary>
/// The month's price per unit, null when the item wasn't bought at a usable price that month, and the number
/// of priced lines behind it (0 for a gap).
/// </summary>
public record PriceMonthDto(string Month, decimal? Price, int PurchaseCount);
