using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class CategoryEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapCategoryEndpoints()
		{
			RouteGroupBuilder categoryGroup = app.MapGroup("/category");

			// GET /category
			categoryGroup.MapGet("/", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				             IQueryable<Category> query = dbContext.Categories
				                                                   .Include(cat => cat.ParentCategory)
				                                                   .Include(cat => cat.ChildCategories)
				                                                   .AsNoTracking();

				             if (!await query.AnyAsync(ct))
					             return Results.NotFound("No categories found");

				             List<CategoryDto> categoryDtos = await query
				                                                    .Where(cat => cat.ParentCategoryId == null)
				                                                    .OrderBy(cat => cat.SortOrder)
				                                                    .ThenBy(cat => cat.DisplayName)
				                                                    .Select(cat => cat.ToDto())
				                                                    .ToListAsync(ct);

				             return Results.Ok(categoryDtos);
			             })
			             .WithName("GetCategories")
			             .Produces<List<CategoryDto>>();

			// GET /category/id
			categoryGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             Category? category = await dbContext.Categories
				                                                 .Include(cat => cat.ChildCategories)
				                                                 .FirstOrDefaultAsync(cat => cat.Id == id, ct);

				             return category != null ? Results.Ok(category.ToDto()) : Results.NotFound($"Category with id {id} not found");

			             })
			             .WithName("GetCategoryById")
			             .Produces<CategoryDto>();

			// POST /category
			categoryGroup.MapPost("/", async (CreateCategoryDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             int? max = await FindSortOrderMax(dto.ParentCategoryId, dbContext, ct);

				             var category = new Category {
					             DisplayName = dto.DisplayName,
					             ParentCategoryId = dto.ParentCategoryId,
					             SortOrder = (max ?? -1)+1
				             };

				             dbContext.Categories.Add(category);
				             await dbContext.SaveChangesAsync(ct);

				             return Results.Created($"/category/{category.Id}", category.ToDto());
			             })
			             .WithName("CreateCategory")
			             .Produces<CategoryDto>();

			// PATCH /category/id
			categoryGroup.MapPatch("/{id:guid}", async (Guid id, UpdateCategoryDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             Category? category = await dbContext.Categories
				                                                 .FirstOrDefaultAsync(cat => cat.Id == id, ct);

				             if (category == null)
					             return Results.NotFound($"Category with id {id} not found");

				             if (!string.IsNullOrWhiteSpace(dto.DisplayName))
					             category.DisplayName = dto.DisplayName;

				             // TODO: Enforce a max category depth
				             if (dto.ParentCategoryId != null && dto.ParentCategoryId != category.ParentCategoryId)
				             {
					             int? max = await FindSortOrderMax(dto.ParentCategoryId, dbContext, ct);

					             category.ParentCategoryId = dto.ParentCategoryId;
					             category.SortOrder = (max ?? -1)+1;
				             }

				             await dbContext.SaveChangesAsync(ct);

				             return Results.Ok(category.ToDto());
			             })
			             .WithName("UpdateCategory")
			             .Produces<CategoryDto>();

			// PUT /category/order
			categoryGroup.MapPut("/order", async (PutCategoryOrderDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             List<Category> siblings = await dbContext.Categories
				                                                      .Where(cat => cat.ParentCategoryId == dto.ParentCategoryId)
				                                                      .ToListAsync(ct);

				             if (siblings.Count != dto.OrderedIds.Count || !siblings.All(cat => dto.OrderedIds.Contains(cat.Id)))
					             return Results.BadRequest("OrderedIds must contain exactly the children of that parent.");

				             for (var i = 0; i < dto.OrderedIds.Count; i++)
					             siblings.First(cat => cat.Id == dto.OrderedIds[i]).SortOrder = i;

				             await dbContext.SaveChangesAsync(ct);

				             return Results.Ok(siblings.Select(cat => cat.ToDto()).OrderBy(cat => cat.SortOrder).ToList());
			             })
			             .WithName("ReorderCategories")
			             .Produces<List<CategoryDto>>();
		}
	}

	private static async Task<int?> FindSortOrderMax(Guid? parentCategoryId, KvitteriaDbContext dbContext, CancellationToken ct)
	{
		int? max = await dbContext.Categories
		                          .Where(cat => cat.ParentCategoryId == parentCategoryId)
		                          .MaxAsync(cat => (int?)cat.SortOrder, ct);
		return max;
	}
}