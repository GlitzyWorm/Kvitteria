using Backend.Features.Insights;

namespace Backend.Tests;

public class PercentTests
{
	[Fact]
	public void A_rise_is_positive() => Assert.Equal(10m, Percent.Change(1000m, 1100m));

	[Fact]
	public void A_fall_is_negative() => Assert.Equal(-25m, Percent.Change(200m, 150m));

	[Fact]
	public void A_zero_starting_value_has_no_change() => Assert.Null(Percent.Change(0m, 500m));

	[Fact]
	public void No_movement_is_zero() => Assert.Equal(0m, Percent.Change(300m, 300m));

	[Fact]
	public void Rounds_to_one_decimal() => Assert.Equal(33.3m, Percent.Change(3m, 4m));
}
