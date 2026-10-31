using Backend.Features.Insights;
using Kvitteria.Contracts.DTO;

namespace Backend.Tests;

public class PriceHistoryTests
{
	private static readonly Guid Item = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
	private static readonly MonthKey Aug = new(2026, 8);

	private static PricedLine Line(int month, decimal quantity, decimal total) =>
		new(new DateTime(2026, month, 10, 12, 0, 0, DateTimeKind.Utc), quantity, total);

	private static PriceHistoryDto Build(int months, params PricedLine[] lines) =>
		PriceHistory.Build(Item, "Hakket oksekød", Aug.RunEndingHere(months), lines);

	[Fact]
	public void A_weighed_item_is_total_over_quantity_not_an_average_of_unit_prices()
	{
		// 19.72 / 1.24 = 15.903 and 12.08 / 0.76 = 15.895; averaging them gives 15.899, the weighted figure is 15.90.
		PriceHistoryDto dto = Build(1, Line(8, 1.24m, 19.72m), Line(8, 0.76m, 12.08m));

		Assert.Equal(15.90m, Assert.Single(dto.Months).Price);
	}

	[Fact]
	public void Several_merchants_in_one_month_average_weighted_by_quantity()
	{
		// 2 for 20 at one shop, 1 for 14 at another: 34 / 3.
		PriceHistoryDto dto = Build(1, Line(8, 2m, 20m), Line(8, 1m, 14m));

		Assert.Equal(11.33m, dto.Months[0].Price);
	}

	[Fact]
	public void Discount_and_zero_quantity_lines_are_excluded()
	{
		PriceHistoryDto dto = Build(1, Line(8, 1m, 10m), Line(8, 1m, -3m), Line(8, 0m, 5m), Line(8, 1m, 0m));

		Assert.Equal(10m, dto.Months[0].Price);
	}

	[Fact]
	public void A_month_with_only_excluded_lines_has_no_price() =>
		Assert.Null(Build(1, Line(8, 1m, -3m)).Months[0].Price);

	[Fact]
	public void A_month_without_a_purchase_is_listed_with_a_null_price()
	{
		PriceHistoryDto dto = Build(3, Line(6, 1m, 10m), Line(8, 1m, 12m));

		Assert.Equal(["2026-06", "2026-07", "2026-08"], dto.Months.Select(m => m.Month));
		Assert.Equal([10m, null, 12m], dto.Months.Select(m => m.Price));
	}

	[Fact]
	public void A_rise_gives_latest_lowest_and_change()
	{
		PriceHistoryDto dto = Build(3, Line(6, 1m, 10m), Line(8, 1m, 11m));

		Assert.Equal(11m, dto.LatestPrice);
		Assert.Equal(10m, dto.LowestPrice);
		Assert.Equal(10m, dto.ChangePercent);
	}

	[Fact]
	public void Lowest_can_be_in_the_middle_of_the_run()
	{
		PriceHistoryDto dto = Build(3, Line(6, 1m, 10m), Line(7, 1m, 8m), Line(8, 1m, 11m));

		Assert.Equal(8m, dto.LowestPrice);
	}

	[Fact]
	public void One_priced_month_is_latest_and_lowest_with_no_change()
	{
		PriceHistoryDto dto = Build(6, Line(7, 1m, 9.5m));

		Assert.Equal(9.5m, dto.LatestPrice);
		Assert.Equal(9.5m, dto.LowestPrice);
		Assert.Null(dto.ChangePercent);
	}

	[Fact]
	public void No_priced_months_gives_nulls_and_still_lists_the_run()
	{
		PriceHistoryDto dto = Build(6);

		Assert.Equal(6, dto.Months.Count);
		Assert.Null(dto.LatestPrice);
		Assert.Null(dto.LowestPrice);
		Assert.Null(dto.ChangePercent);
	}

	[Fact]
	public void Purchases_outside_the_run_are_ignored() =>
		Assert.Null(Build(2, Line(5, 1m, 10m)).LatestPrice);

	[Fact]
	public void Carries_the_item_identity()
	{
		PriceHistoryDto dto = Build(1);

		Assert.Equal(Item, dto.ItemId);
		Assert.Equal("Hakket oksekød", dto.DisplayName);
	}
}
