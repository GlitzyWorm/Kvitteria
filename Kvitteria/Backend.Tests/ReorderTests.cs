using Microsoft.AspNetCore.Http;
using Backend.Features.Shared;
namespace Backend.Tests;

public class ReorderTests
{
	private sealed record Item(Guid Id)
	{
		public int SortOrder { get; set; } = -1;
	}

	private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
	private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
	private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");

	private static bool Apply(IReadOnlyList<Item> items, IReadOnlyList<Guid>? ids, out IResult problem) =>
		Reorder.TryApply(items, ids, i => i.Id, (i, order) => i.SortOrder = order, out problem);

	[Fact]
	public void Assigns_each_id_its_index()
	{
		Item a = new(A), b = new(B), c = new(C);

		Assert.True(Apply([a, b, c], [C, A, B], out _));

		Assert.Equal(1, a.SortOrder);
		Assert.Equal(2, b.SortOrder);
		Assert.Equal(0, c.SortOrder);
	}

	/// <summary>A missing property deserialized to null and produced a 500 before this guard.</summary>
	[Fact]
	public void Rejects_a_null_list() =>
		Assert.False(Apply([new Item(A)], null, out _));

	[Fact]
	public void Rejects_an_empty_list() =>
		Assert.False(Apply([new Item(A)], [], out _));

	[Fact]
	public void Rejects_a_partial_list() =>
		Assert.False(Apply([new Item(A), new Item(B)], [A], out _));

	[Fact]
	public void Rejects_an_unknown_id() =>
		Assert.False(Apply([new Item(A), new Item(B)], [A, C], out _));

	[Fact]
	public void Rejects_duplicates_even_though_the_count_matches() =>
		Assert.False(Apply([new Item(A), new Item(B)], [A, A], out _));

	[Fact]
	public void Leaves_sort_order_untouched_when_it_rejects()
	{
		var a = new Item(A);

		Assert.False(Apply([a], [C], out _));
		Assert.Equal(-1, a.SortOrder);
	}
}
