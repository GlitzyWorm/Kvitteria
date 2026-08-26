using Kvitteria.Contracts.Shared;

namespace Backend.Tests;

public class PagingTests
{
	[Theory]
	[InlineData(1, 10, true)]
	[InlineData(1, 1, true)]
	[InlineData(5, 100, true)]
	[InlineData(0, 10, false)]
	[InlineData(-1, 10, false)]
	[InlineData(1, 0, false)]
	[InlineData(1, 101, false)]
	public void Accepts_only_pages_within_range(int page, int pageSize, bool expected) =>
		Assert.Equal(expected, Paging.TryValidate(page, pageSize, out _));
}
