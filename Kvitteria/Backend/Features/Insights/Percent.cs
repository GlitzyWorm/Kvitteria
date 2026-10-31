namespace Backend.Features.Insights;

public static class Percent
{
	/// <summary>
	/// The change from <paramref name="from"/> to <paramref name="to"/> as a percentage of
	/// <paramref name="from"/>, to one decimal. Null when there is nothing to compare against.
	/// </summary>
	public static decimal? Change(decimal from, decimal to) =>
		from == 0 ? null : Math.Round((to - from) / from * 100, 1, MidpointRounding.AwayFromZero);
}
