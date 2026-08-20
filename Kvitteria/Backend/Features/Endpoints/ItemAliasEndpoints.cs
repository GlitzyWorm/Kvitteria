using Backend.Features.Auth;
using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class ItemAliasEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapItemAliasEndpoints()
		{
			RouteGroupBuilder aliasGroup = app.MapGroup("/aliases");

			// GET /aliases?search=&canonicalId&page=&pageSize=
			aliasGroup.MapGet("/", async (string? search, Guid? canonicalItemId, KvitteriaDbContext dbContext, CancellationToken ct, int pageSize = 10, int page = 1) => {
				          if (!Paging.TryValidate(page, pageSize, out IResult pagingProblem))
					          return pagingProblem;

				          IQueryable<ItemAlias> query = dbContext.ItemAliases
				                                                 .Include(ia => ia.CanonicalItem)
				                                                 .Where(ia => (search == null || ia.RawName.Contains(search)) && (canonicalItemId == null || ia.CanonicalItemId == canonicalItemId))
				                                                 .AsNoTracking();

				          int totalCount = await query.CountAsync(ct);

				          List<ItemAliasDto> itemAliasDtos = await query
				                                                   .OrderBy(ia => ia.RawName)
				                                                   .Skip((page-1)*pageSize)
				                                                   .Take(pageSize)
				                                                   .Select(ia => ia.ToDto())
				                                                   .ToListAsync(ct);

				          return Results.Ok(new PagedResult<ItemAliasDto>(itemAliasDtos, page, pageSize, totalCount));
			          })
			          .WithName("GetItemAliases")
			          .Produces<PagedResult<ItemAliasDto>>()
			          .ProducesProblem(StatusCodes.Status400BadRequest);

			// DELETE /aliases/{id}
			aliasGroup.MapDelete("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				int deleted = await dbContext.ItemAliases
				                            .Where(ia => ia.Id == id)
				                            .ExecuteDeleteAsync(ct);

				if (deleted == 0)
					return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Item alias with id {id} not found");

				return Results.NoContent();
			})
			.WithName("DeleteItemAlias")
			.RequireAuthorization(PermissionPolicies.Name(Permission.ManageTaxonomy))
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status404NotFound);
		}
	}
}
