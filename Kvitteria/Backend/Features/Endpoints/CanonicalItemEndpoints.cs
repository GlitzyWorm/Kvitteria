using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class CanonicalItemEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapCanonicalItemEndpoints()
		{
			RouteGroupBuilder canonicalItemGroup = app.MapGroup("/canonical-items");

			// GET /canonical-items?search=&page=&pageSize=
			canonicalItemGroup.MapGet("/", async (string? search, KvitteriaDbContext dbContext, CancellationToken ct, int pageSize = 10, int page = 1) => {
				                  if (page < 1 || pageSize < 1 || pageSize > 100)
					                  return Results.BadRequest("page must be >= 1 and pageSize must be between 1 and 100");

				                  IQueryable<CanonicalItem> query = dbContext.CanonicalItems
				                                                             .Where(ci => (search == null || ci.DisplayName.Contains(search)))
				                                                             .AsNoTracking();

				                  int totalCount = await query.CountAsync(ct);

				                  if (totalCount == 0)
					                  return Results.NotFound("No canonical items found");

				                  List<CanonicalItemDto> canonicalItemDtos = await query
				                                                                   .OrderBy(ci => ci.DisplayName)
				                                                                   .Skip((page-1)*pageSize)
				                                                                   .Take(pageSize)
				                                                                   .Select(ci => ci.ToDto())
				                                                                   .ToListAsync(ct);

				                  return Results.Ok(new PagedResult<CanonicalItemDto>(canonicalItemDtos, page, pageSize, totalCount));
			                  })
			                  .WithName("GetCanonicalItems")
			                  .Produces<PagedResult<CanonicalItemDto>>();

			// GET /canonical-items/{id}
			canonicalItemGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                  CanonicalItem? canonicalItem = await dbContext.CanonicalItems
				                                                                .AsNoTracking()
				                                                                .FirstOrDefaultAsync(ci => ci.Id == id, ct);

				                  return canonicalItem != null ? Results.Ok(canonicalItem.ToDto()) : Results.NotFound($"Canonical item with id {id} not found");
			                  })
			                  .WithName("GetCanonicalItemById")
			                  .Produces<CanonicalItemDto>();

			// POST /canonical-items
			canonicalItemGroup.MapPost("/", async (CreateCanonicalItemDto canonicalItemDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                  if (string.IsNullOrWhiteSpace(canonicalItemDto.DisplayName))
					                  return Results.BadRequest("DisplayName is required");

				                  var canonicalItem = new CanonicalItem {
					                  DisplayName = canonicalItemDto.DisplayName
				                  };

				                  dbContext.CanonicalItems.Add(canonicalItem);
				                  await dbContext.SaveChangesAsync(ct);

				                  return Results.Created($"/canonical-items/{canonicalItem.Id}", canonicalItem.ToDto());
			                  })
			                  .WithName("CreateCanonicalItem")
			                  .Produces<CanonicalItemDto>();

			// PATCH /canonical-items/{id}
			canonicalItemGroup.MapPatch("/{id:guid}", async (Guid id, CreateCanonicalItemDto canonicalItemDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                  CanonicalItem? canonicalItem = await dbContext.CanonicalItems
				                                                                .FirstOrDefaultAsync(ci => ci.Id == id, ct);

				                  if (canonicalItem == null)
					                  return Results.NotFound($"Canonical item with id {id} not found");

				                  if (string.IsNullOrWhiteSpace(canonicalItemDto.DisplayName))
					                  return Results.BadRequest("DisplayName is required");

				                  canonicalItem.DisplayName = canonicalItemDto.DisplayName;
				                  await dbContext.SaveChangesAsync(ct);

				                  return Results.Ok(canonicalItem.ToDto());
			                  })
			                  .WithName("UpdateCanonicalItem")
			                  .Produces<CanonicalItemDto>();

			// TODO: Implement DELETE /canonical-items/{id} endpoint if needed
		}
	}
}
