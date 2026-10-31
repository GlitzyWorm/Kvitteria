using Backend.Features.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Categories;

public sealed record CategoryNode(Guid Id, Guid? ParentId, string DisplayName, int SortOrder);

/// <summary>
/// The whole category taxonomy in memory, with the structural rules as pure functions.
/// Loads every category in one query, which assumes the taxonomy stays small and bounded by
/// <see cref="MaxDepth"/>.
/// </summary>
public sealed class CategoryTree
{
	/// <summary>Root (1) -> child (2) -> grandchild (3). A grandchild cannot have children.</summary>
	public const int MaxDepth = 3;

	/// <summary>Stands in for "no parent" so the child index can use a non-nullable key.</summary>
	private static readonly Guid RootParentKey = Guid.Empty;

	private readonly Dictionary<Guid, CategoryNode> _byId;
	private readonly Dictionary<Guid, List<CategoryNode>> _byParent;

	public CategoryTree(IEnumerable<CategoryNode> nodes)
	{
		List<CategoryNode> all = nodes.ToList();
		_byId = all.ToDictionary(n => n.Id);
		_byParent = all.GroupBy(n => n.ParentId ?? RootParentKey).ToDictionary(g => g.Key, g => g.ToList());
	}

	public static async Task<CategoryTree> LoadAsync(KvitteriaDbContext dbContext, CancellationToken ct) =>
		new(await dbContext.Categories
		                   .AsNoTracking()
		                   .Select(c => new CategoryNode(c.Id, c.ParentCategoryId, c.DisplayName, c.SortOrder))
		                   .ToListAsync(ct));

	public bool Contains(Guid id) => _byId.ContainsKey(id);

	public CategoryNode? Node(Guid id) => _byId.GetValueOrDefault(id);

	/// <summary>Depth counted from the root, where a root is 1. Null when the category does not exist.</summary>
	public int? Depth(Guid id)
	{
		if (!_byId.TryGetValue(id, out CategoryNode? node))
			return null;

		var depth = 1;

		// Bounded so cyclic data cannot spin forever.
		while (node.ParentId != null && depth <= MaxDepth)
		{
			if (!_byId.TryGetValue(node.ParentId.Value, out CategoryNode? parent))
				break;

			node = parent;
			depth++;
		}

		return depth;
	}

	/// <summary>Number of levels in the subtree rooted at this category; a leaf is 1.</summary>
	public int SubtreeHeight(Guid id)
	{
		List<Guid> level = [id];
		var height = 1;

		while (height <= MaxDepth)
		{
			List<Guid> children = level
				.SelectMany(parentId => Children(parentId))
				.Select(n => n.Id)
				.ToList();

			if (children.Count == 0)
				break;

			height++;
			level = children;
		}

		return height;
	}

	/// <summary>True when <paramref name="candidateId"/> is the category itself or one of its descendants.</summary>
	public bool IsSelfOrDescendantOf(Guid candidateId, Guid ancestorId)
	{
		Guid? currentId = candidateId;

		for (var i = 0; i <= MaxDepth && currentId != null; i++)
		{
			if (currentId == ancestorId)
				return true;

			currentId = _byId.TryGetValue(currentId.Value, out CategoryNode? node) ? node.ParentId : null;
		}

		return false;
	}

	public int? MaxSortOrder(Guid? parentId)
	{
		List<CategoryNode> siblings = Children(parentId);
		return siblings.Count == 0 ? null : siblings.Max(n => n.SortOrder);
	}

	/// <summary>
	/// Case-insensitive, matching the citext unique indexes. This is a fast pre-check that yields a
	/// clear message; the database constraint remains the actual guarantee.
	/// </summary>
	public bool IsSiblingNameTaken(Guid? parentId, string displayName, Guid? excludeId) =>
		Children(parentId).Any(n => n.Id != excludeId
		                            && string.Equals(n.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));

	private List<CategoryNode> Children(Guid? parentId) =>
		_byParent.TryGetValue(parentId ?? RootParentKey, out List<CategoryNode>? children) ? children : [];
}
