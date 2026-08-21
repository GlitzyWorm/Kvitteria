using Backend.Features.Auth;
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
				                  if (!Paging.TryValidate(page, pageSize, out IResult pagingProblem))
					                  return pagingProblem;

				                  IQueryable<CanonicalItem> query = dbContext.CanonicalItems
				                                                             .Where(ci => (search == null || ci.DisplayName.Contains(search)))
				                                                             .AsNoTracking();

				                  int totalCount = await query.CountAsync(ct);

				                  List<CanonicalItemDto> canonicalItemDtos = await query
				                                                                   .OrderBy(ci => ci.DisplayName)
				                                                                   .Skip((page-1)*pageSize)
				                                                                   .Take(pageSize)
				                                                                   .Select(ci => ci.ToDto())
				                                                                   .ToListAsync(ct);

				                  return Results.Ok(new PagedResult<CanonicalItemDto>(canonicalItemDtos, page, pageSize, totalCount));
			                  })
			                  .WithName("GetCanonicalItems")
			                  .Produces<PagedResult<CanonicalItemDto>>()
			                  .ProducesProblem(StatusCodes.Status400BadRequest);

			// GET /canonical-items/{id}
			canonicalItemGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                  CanonicalItem? canonicalItem = await dbContext.CanonicalItems
				                                                                .AsNoTracking()
				                                                                .FirstOrDefaultAsync(ci => ci.Id == id, ct);

				                  return canonicalItem != null ? Results.Ok(canonicalItem.ToDto()) : Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Canonical item with id {id} not found");
			                  })
			                  .WithName("GetCanonicalItemById")
			                  .Produces<CanonicalItemDto>()
			                  .ProducesProblem(StatusCodes.Status404NotFound);

			// POST /canonical-items
			canonicalItemGroup.MapPost("/", async (CreateCanonicalItemDto canonicalItemDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                  if (string.IsNullOrWhiteSpace(canonicalItemDto.DisplayName))
					                  return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"DisplayName is required");

				                  var canonicalItem = new CanonicalItem {
					                  DisplayName = canonicalItemDto.DisplayName
				                  };

				                  dbContext.CanonicalItems.Add(canonicalItem);
				                  if (!await dbContext.TrySaveChangesAsync(ct))
					                  return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A canonical item named '{canonicalItemDto.DisplayName}' already exists.");

				                  return Results.CreatedAtRoute("GetCanonicalItemById", new { id = canonicalItem.Id }, canonicalItem.ToDto());
			                  })
			                  .WithName("CreateCanonicalItem")
			                  .RequireAuthorization(PermissionPolicies.Name(Permission.ManageTaxonomy))
			                  .Produces<CanonicalItemDto>(StatusCodes.Status201Created)
			                  .ProducesProblem(StatusCodes.Status400BadRequest)
			                  .ProducesProblem(StatusCodes.Status409Conflict);

			// PATCH /canonical-items/{id}
			canonicalItemGroup.MapPatch("/{id:guid}", async (Guid id, UpdateCanonicalItemDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                  CanonicalItem? canonicalItem = await dbContext.CanonicalItems
				                                                                .FirstOrDefaultAsync(ci => ci.Id == id, ct);

				                  if (canonicalItem == null)
					                  return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Canonical item with id {id} not found");

				                  if (string.IsNullOrWhiteSpace(dto.DisplayName))
					                  return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"DisplayName is required");
				                  
				                  switch (dto)
				                  {
					                  case { ClearCategory: true, CategoryId: not null }:
						                  return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"ClearCategory cannot be combined with a CategoryId.");
					                  case { ClearDefaultMeal: true, DefaultMealId: not null }:
						                  return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"ClearDefaultMeal cannot be combined with a DefaultMealId.");
				                  }

				                  if (dto.CategoryId != null &&
				                      !await dbContext.Categories.AnyAsync(cat => cat.Id == dto.CategoryId, ct))
					                  return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Category with id {dto.CategoryId} not found");

				                  if (dto.DefaultMealId != null &&
				                      !await dbContext.Meals.AnyAsync(m => m.Id == dto.DefaultMealId, ct))
					                  return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Meal with id {dto.DefaultMealId} not found");

				                  canonicalItem.DisplayName = dto.DisplayName;

				                  if (dto.ClearCategory)
					                  canonicalItem.CategoryId = null;
				                  else if (dto.CategoryId != null)
					                  canonicalItem.CategoryId = dto.CategoryId;

				                  if (dto.ClearDefaultMeal)
					                  canonicalItem.DefaultMealId = null;
				                  else if (dto.DefaultMealId != null)
					                  canonicalItem.DefaultMealId = dto.DefaultMealId;

				                  if (!await dbContext.TrySaveChangesAsync(ct))
					                  return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A canonical item named '{dto.DisplayName}' already exists.");

				                  return Results.Ok(canonicalItem.ToDto());
			                  })
			                  .WithName("UpdateCanonicalItem")
			                  .RequireAuthorization(PermissionPolicies.Name(Permission.ManageTaxonomy))
			                  .Produces<CanonicalItemDto>()
			                  .ProducesProblem(StatusCodes.Status400BadRequest)
			                  .ProducesProblem(StatusCodes.Status404NotFound)
			                  .ProducesProblem(StatusCodes.Status409Conflict);

			// TODO: Implement DELETE /canonical-items/{id} endpoint if needed
		}
	}
}
