using Backend.Features.Receipts;
using Kvitteria.Contracts.DTO;
namespace Backend.Tests;

public class LineSortingTests
{
	private static SortReceiptLineDto Request(Guid? itemId, string? newName) =>
		new(itemId, newName, null, null, false, false);

	[Fact]
	public void Rejects_neither_item() =>
		Assert.False(LineSorting.TryValidate(Request(null, null), out _));

	[Fact]
	public void Rejects_both_items() =>
		Assert.False(LineSorting.TryValidate(Request(Guid.NewGuid(), "Mælk"), out _));

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_a_blank_name(string name) =>
		Assert.False(LineSorting.TryValidate(Request(null, name), out _));

	[Fact]
	public void Accepts_200_characters() =>
		Assert.True(LineSorting.TryValidate(Request(null, new string('a', 200)), out _));

	[Fact]
	public void Rejects_201_characters() =>
		Assert.False(LineSorting.TryValidate(Request(null, new string('a', 201)), out _));

	[Fact]
	public void Accepts_an_existing_item() =>
		Assert.True(LineSorting.TryValidate(Request(Guid.NewGuid(), null), out _));

	[Fact]
	public void Accepts_a_new_item_name() =>
		Assert.True(LineSorting.TryValidate(Request(null, "Mælk"), out _));
}
