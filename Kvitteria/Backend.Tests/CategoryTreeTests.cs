using Backend.Features.Categories;
namespace Backend.Tests;

public class CategoryTreeTests
{
	private static readonly Guid Root = Guid.Parse("00000000-0000-0000-0000-000000000001");
	private static readonly Guid Child = Guid.Parse("00000000-0000-0000-0000-000000000002");
	private static readonly Guid Grandchild = Guid.Parse("00000000-0000-0000-0000-000000000003");
	private static readonly Guid OtherRoot = Guid.Parse("00000000-0000-0000-0000-000000000004");
	private static readonly Guid Unknown = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

	private static CategoryTree Tree() => new([
		new CategoryNode(Root, null, "Fødevarer", 0),
		new CategoryNode(Child, Root, "Kød & Fisk", 0),
		new CategoryNode(Grandchild, Child, "Hakket", 0),
		new CategoryNode(OtherRoot, null, "Drikkevarer", 1)
	]);

	[Fact]
	public void Depth_counts_from_one_at_the_root()
	{
		CategoryTree tree = Tree();

		Assert.Equal(1, tree.Depth(Root));
		Assert.Equal(2, tree.Depth(Child));
		Assert.Equal(3, tree.Depth(Grandchild));
	}

	[Fact]
	public void Depth_is_null_for_an_unknown_category() =>
		Assert.Null(Tree().Depth(Unknown));

	[Fact]
	public void Subtree_height_is_one_for_a_leaf()
	{
		CategoryTree tree = Tree();

		Assert.Equal(1, tree.SubtreeHeight(Grandchild));
		Assert.Equal(2, tree.SubtreeHeight(Child));
		Assert.Equal(3, tree.SubtreeHeight(Root));
	}

	/// <summary>The rule that keeps POST/PATCH from exceeding MaxDepth.</summary>
	[Fact]
	public void Depth_plus_subtree_height_detects_an_over_deep_move()
	{
		CategoryTree tree = Tree();

		// Moving Root (height 3) under Child (depth 2) would produce depth 5.
		Assert.Equal(5, tree.Depth(Child)!.Value+tree.SubtreeHeight(Root));
		// Moving Grandchild (height 1) under OtherRoot (depth 1) produces depth 2, which is fine.
		Assert.Equal(2, tree.Depth(OtherRoot)!.Value+tree.SubtreeHeight(Grandchild));
	}

	[Fact]
	public void Descendant_check_includes_the_category_itself()
	{
		CategoryTree tree = Tree();

		Assert.True(tree.IsSelfOrDescendantOf(Root, Root));
		Assert.True(tree.IsSelfOrDescendantOf(Grandchild, Root));
		Assert.True(tree.IsSelfOrDescendantOf(Child, Root));
		Assert.False(tree.IsSelfOrDescendantOf(Root, Grandchild));
		Assert.False(tree.IsSelfOrDescendantOf(OtherRoot, Root));
	}

	[Fact]
	public void Sibling_names_are_compared_case_insensitively_like_the_citext_index()
	{
		CategoryTree tree = Tree();

		Assert.True(tree.IsSiblingNameTaken(Root, "kød & fisk", null));
		Assert.True(tree.IsSiblingNameTaken(null, "FØDEVARER", null));
		// Excluding the row being renamed lets a category keep its own name.
		Assert.False(tree.IsSiblingNameTaken(Root, "Kød & Fisk", Child));
		// Roots and children are separate namespaces, matching the filtered unique indexes.
		Assert.False(tree.IsSiblingNameTaken(null, "Kød & Fisk", null));
	}

	[Fact]
	public void Max_sort_order_is_null_when_there_are_no_children()
	{
		CategoryTree tree = Tree();

		Assert.Equal(1, tree.MaxSortOrder(null));
		Assert.Equal(0, tree.MaxSortOrder(Root));
		Assert.Null(tree.MaxSortOrder(Grandchild));
	}

	/// <summary>
	/// Pre-existing cyclic rows must not hang a request. The walks are bounded by MaxDepth for exactly
	/// this reason.
	/// </summary>
	[Fact]
	public void Cyclic_data_terminates()
	{
		var cyclic = new CategoryTree([
			new CategoryNode(Root, Child, "A", 0),
			new CategoryNode(Child, Root, "B", 0)
		]);

		Assert.NotNull(cyclic.Depth(Root));
		Assert.True(cyclic.SubtreeHeight(Root) > 0);
		Assert.True(cyclic.IsSelfOrDescendantOf(Child, Root));
	}
}
