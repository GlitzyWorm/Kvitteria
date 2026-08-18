using Backend.Features.Categories;
using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class CategoryEndpoints
{
	private const int MaxDepth = CategoryTree.MaxDepth;

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

				             // One projection of the whole taxonomy replaces the per-level depth walk, the sibling-name
				             // query and the sort-order query.
				             CategoryTree tree = await CategoryTree.LoadAsync(dbContext, ct);

				             if (dto.ParentCategoryId != null)
				             {
					             switch (tree.Depth(dto.ParentCategoryId.Value))
					             {
						             case null:
							             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Parent category with id {dto.ParentCategoryId} not found");
						             case >= MaxDepth:
							             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"Cannot create a category under {dto.ParentCategoryId}: maximum depth of {MaxDepth} would be exceeded.");
					             }
				             }

				             if (tree.IsSiblingNameTaken(dto.ParentCategoryId, displayName, null))
					             return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A category named '{displayName}' already exists under this parent.");

				             int? max = tree.MaxSortOrder(dto.ParentCategoryId);

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

				             CategoryTree tree = await CategoryTree.LoadAsync(dbContext, ct);

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
					             if (tree.IsSelfOrDescendantOf(newParentId, id))
						             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"Cannot move a category under its own descendant.");

					             int? parentDepth = tree.Depth(newParentId);

					             if (parentDepth == null)
						             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Parent category with id {newParentId} not found");

					             int resultingDepth = parentDepth.Value+tree.SubtreeHeight(id);

					             if (resultingDepth > MaxDepth)
						             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"Cannot move category: resulting depth would be {resultingDepth}, maximum is {MaxDepth}.");
				             }

				             if ((isReparenting || displayName != category.DisplayName)
				                 && tree.IsSiblingNameTaken(targetParentId, displayName, id))
					             return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A category named '{displayName}' already exists under this parent.");

				             category.DisplayName = displayName;

				             if (isReparenting)
				             {
					             int? max = tree.MaxSortOrder(targetParentId);

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
}
