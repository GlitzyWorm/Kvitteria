using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class CategoryEndpoints
{
	// Root (1) -> child (2) -> grandchild (3). A grandchild cannot have children.
	private const int MaxDepth = 3;

	extension(RouteGroupBuilder app)
	{
		internal void MapCategoryEndpoints()
		{
			RouteGroupBuilder categoryGroup = app.MapGroup("/category");

			// GET /category
			categoryGroup.MapGet("/", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				             List<Category> roots = await dbContext.Categories
				                                                   .Include(cat => cat.ChildCategories)
				                                                   .ThenInclude(cat => cat.ChildCategories)
				                                                   .AsNoTracking()
				                                                   .Where(cat => cat.ParentCategoryId == null)
				                                                   .OrderBy(cat => cat.SortOrder)
				                                                   .ThenBy(cat => cat.DisplayName)
				                                                   .ToListAsync(ct);

				             return Results.Ok(roots.Select(cat => cat.ToDto()).ToList());

			             })
			             .WithName("GetCategories")
			             .Produces<List<CategoryDto>>();

			// GET /category/id
			categoryGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             Category? category = await dbContext.Categories
				                                                 .Include(cat => cat.ChildCategories)
				                                                 .ThenInclude(cat => cat.ChildCategories)
				                                                 .AsNoTracking()
				                                                 .FirstOrDefaultAsync(cat => cat.Id == id, ct);

				             return category != null
				             ? Results.Ok(category.ToDto())
				             : Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Category with id {id} not found");
			             })
			             .WithName("GetCategoryById")
			             .Produces<CategoryDto>()
			             .ProducesProblem(StatusCodes.Status404NotFound);

			// POST /category
			categoryGroup.MapPost("/", async (CreateCategoryDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             if (string.IsNullOrWhiteSpace(dto.DisplayName))
					             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"DisplayName is required.");

				             string displayName = dto.DisplayName.Trim();

				             if (dto.ParentCategoryId != null)
				             {
					             int? parentDepth = await FindDepth(dto.ParentCategoryId.Value, dbContext, ct);

					             switch (parentDepth)
					             {
						             case null:
							             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Parent category with id {dto.ParentCategoryId} not found");
						             case >= MaxDepth:
							             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"Cannot create a category under {dto.ParentCategoryId}: maximum depth of {MaxDepth} would be exceeded.");
					             }

				             }

				             if (await IsSiblingNameTaken(dto.ParentCategoryId, displayName, null, dbContext, ct))
					             return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A category named '{displayName}' already exists under this parent.");

				             int? max = await FindSortOrderMax(dto.ParentCategoryId, dbContext, ct);

				             var category = new Category {
					             DisplayName = displayName,
					             ParentCategoryId = dto.ParentCategoryId,
					             SortOrder = (max ?? -1)+1
				             };

				             dbContext.Categories.Add(category);

				             if (!await dbContext.TrySaveChangesAsync(ct))
					             return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A category named '{displayName}' already exists under this parent.");

				             return Results.Created($"/category/{category.Id}", category.ToDto());
			             })
			             .WithName("CreateCategory")
			             .Produces<CategoryDto>()
			             .ProducesProblem(StatusCodes.Status400BadRequest)
			             .ProducesProblem(StatusCodes.Status404NotFound)
			             .ProducesProblem(StatusCodes.Status409Conflict);

			// PATCH /category/id
			categoryGroup.MapPatch("/{id:guid}", async (Guid id, UpdateCategoryDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             Category? category = await dbContext.Categories
				                                                 .FirstOrDefaultAsync(cat => cat.Id == id, ct);

				             if (category == null)
					             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Category with id {id} not found");

				             if (dto is { ClearParent: true, ParentCategoryId: not null })
					             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"ClearParent cannot be combined with a ParentCategoryId.");

				             string displayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? category.DisplayName : dto.DisplayName.Trim();

				             // ClearParent promotes to root; otherwise a null ParentCategoryId means "leave the parent alone".
				             Guid? targetParentId = dto.ClearParent ? null : dto.ParentCategoryId ?? category.ParentCategoryId;
				             bool isReparenting = targetParentId != category.ParentCategoryId;

				             // Promoting to root can only shrink the tree, so it needs neither a cycle nor a depth check.
				             if (isReparenting && targetParentId != null)
				             {
					             Guid newParentId = targetParentId.Value;

					             if (newParentId == id)
						             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"A category cannot be its own parent.");

					             // Checked before the depth walk: a cycle would make the ancestor chain meaningless.
					             if (await IsDescendantOf(newParentId, id, dbContext, ct))
						             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"Cannot move a category under its own descendant.");

					             int? parentDepth = await FindDepth(newParentId, dbContext, ct);

					             if (parentDepth == null)
						             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Parent category with id {newParentId} not found");

					             int resultingDepth = parentDepth.Value+await FindSubtreeHeight(id, dbContext, ct);

					             if (resultingDepth > MaxDepth)
						             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"Cannot move category: resulting depth would be {resultingDepth}, maximum is {MaxDepth}.");
				             }

				             if ((isReparenting || displayName != category.DisplayName)
				                 && await IsSiblingNameTaken(targetParentId, displayName, id, dbContext, ct))
					             return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A category named '{displayName}' already exists under this parent.");

				             category.DisplayName = displayName;

				             if (isReparenting)
				             {
					             int? max = await FindSortOrderMax(targetParentId, dbContext, ct);

					             category.ParentCategoryId = targetParentId;
					             category.SortOrder = (max ?? -1)+1;
				             }

				             if (!await dbContext.TrySaveChangesAsync(ct))
					             return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A category named '{displayName}' already exists under this parent.");

				             return Results.Ok(category.ToDto());
			             })
			             .WithName("UpdateCategory")
			             .Produces<CategoryDto>()
			             .ProducesProblem(StatusCodes.Status400BadRequest)
			             .ProducesProblem(StatusCodes.Status404NotFound)
			             .ProducesProblem(StatusCodes.Status409Conflict);

			// PUT /category/order
			categoryGroup.MapPut("/order", async (PutCategoryOrderDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             if (dto.OrderedIds == null || dto.OrderedIds.Count == 0)
					             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"OrderedIds is required and must not be empty.");

				             // Without this an unknown parent yields zero siblings, which used to pass the
				             // count check whenever OrderedIds was also empty and return 200 with [].
				             if (dto.ParentCategoryId != null &&
				                 !await dbContext.Categories.AnyAsync(cat => cat.Id == dto.ParentCategoryId, ct))
					             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Category with id {dto.ParentCategoryId} not found");

				             List<Category> siblings = await dbContext.Categories
				                                                      .Where(cat => cat.ParentCategoryId == dto.ParentCategoryId)
				                                                      .ToListAsync(ct);

				             if (siblings.Count != dto.OrderedIds.Count || !siblings.All(cat => dto.OrderedIds.Contains(cat.Id)))
					             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"OrderedIds must contain exactly the children of that parent.");

				             Dictionary<Guid, Category> siblingsById = siblings.ToDictionary(cat => cat.Id);

				             for (var i = 0; i < dto.OrderedIds.Count; i++)
					             siblingsById[dto.OrderedIds[i]].SortOrder = i;

				             await dbContext.SaveChangesAsync(ct);

				             return Results.Ok(siblings.Select(cat => cat.ToDto()).OrderBy(cat => cat.SortOrder).ToList());
			             })
			             .WithName("ReorderCategories")
			             .Produces<List<CategoryDto>>()
			             .ProducesProblem(StatusCodes.Status400BadRequest)
			             .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}

	private static async Task<int?> FindSortOrderMax(Guid? parentCategoryId, KvitteriaDbContext dbContext, CancellationToken ct)
	{
		int? max = await dbContext.Categories
		                          .Where(cat => cat.ParentCategoryId == parentCategoryId)
		                          .MaxAsync(cat => (int?)cat.SortOrder, ct);
		return max;
	}

	/// <summary>Depth of the category counted from the root (a root is 1), or null when it does not exist.</summary>
	private static async Task<int?> FindDepth(Guid categoryId, KvitteriaDbContext dbContext, CancellationToken ct)
	{
		Guid? currentId = categoryId;
		var depth = 0;

		// Bounded so that pre-existing cyclic data cannot hang the request.
		while (currentId != null && depth <= MaxDepth)
		{
			Guid lookupId = currentId.Value;

			var current = await dbContext.Categories
			                             .Where(cat => cat.Id == lookupId)
			                             .Select(cat => new { cat.ParentCategoryId })
			                             .FirstOrDefaultAsync(ct);

			if (current == null)
				return depth == 0 ? null : depth;

			depth++;
			currentId = current.ParentCategoryId;
		}

		return depth;
	}

	/// <summary>Number of levels in the subtree rooted at the category; a leaf is 1.</summary>
	private static async Task<int> FindSubtreeHeight(Guid categoryId, KvitteriaDbContext dbContext, CancellationToken ct)
	{
		List<Guid> level = [categoryId];
		var height = 1;

		while (height <= MaxDepth)
		{
			List<Guid> children = await dbContext.Categories
			                                     .Where(cat => cat.ParentCategoryId != null && level.Contains(cat.ParentCategoryId.Value))
			                                     .Select(cat => cat.Id)
			                                     .ToListAsync(ct);

			if (children.Count == 0)
				break;

			height++;
			level = children;
		}

		return height;
	}

	/// <summary>True when the candidate parent is the category itself or one of its descendants.</summary>
	private static async Task<bool> IsDescendantOf(Guid candidateParentId, Guid categoryId, KvitteriaDbContext dbContext, CancellationToken ct)
	{
		Guid? currentId = candidateParentId;

		for (var i = 0; i <= MaxDepth && currentId != null; i++)
		{
			if (currentId == categoryId)
				return true;

			Guid lookupId = currentId.Value;

			currentId = await dbContext.Categories
			                           .Where(cat => cat.Id == lookupId)
			                           .Select(cat => cat.ParentCategoryId)
			                           .FirstOrDefaultAsync(ct);
		}

		return false;
	}

	private static async Task<bool> IsSiblingNameTaken(Guid? parentCategoryId, string displayName, Guid? excludeId, KvitteriaDbContext dbContext, CancellationToken ct)
	{
		// DisplayName is citext, so this matches the case-insensitive unique indexes on Category.
		return await dbContext.Categories
		                      .AnyAsync(cat => cat.ParentCategoryId == parentCategoryId
		                                       && cat.DisplayName == displayName
		                                       && (excludeId == null || cat.Id != excludeId), ct);
	}

	/// <summary>Backstop for two requests racing past the sibling-name check. False on a unique violation.</summary>
}