using Backend.Features.Endpoints.ReceiptEndpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints.ReceiptEndpoints;

static class ItemAliasEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapItemAliasEndpoints()
		{
			RouteGroupBuilder aliasGroup = app.MapGroup("/aliases");

			// GET /aliases?search=&canonicalId&page=&pageSize=
			aliasGroup.MapGet("/", async (string? search, Guid? canonicalItemId, KvitteriaDbContext dbContext, CancellationToken ct, int pageSize = 10, int page = 1) => {
				          if (page < 1 || pageSize < 1 || pageSize > 100)
					          return Results.BadRequest("page must be >= 1 and pageSize must be between 1 and 100");

				          IQueryable<ItemAlias> query = dbContext.ItemAliases
				                                                 .Include(ia => ia.CanonicalItem)
				                                                 .Where(ia => (search == null || ia.RawName.Contains(search)) && (canonicalItemId == null || ia.CanonicalItemId == canonicalItemId))
				                                                 .AsNoTracking();

				          int totalCount = await query.CountAsync(ct);

				          if (totalCount == 0)
					          return Results.NotFound("No item aliases found");

				          List<ItemAliasDto> itemAliasDtos = await query
				                                                   .OrderBy(ia => ia.RawName)
				                                                   .Skip((page-1)*pageSize)
				                                                   .Take(pageSize)
				                                                   .Select(ia => ia.ToDto())
				                                                   .ToListAsync(ct);

				          return Results.Ok(new PagedResult<ItemAliasDto>(itemAliasDtos, page, pageSize, totalCount));
			          })
			          .WithName("GetItemAliases")
			          .Produces<PagedResult<ItemAliasDto>>();

			// DELETE /aliases/{id}
			aliasGroup.MapDelete("/{aliasId:guid}", async (Guid aliasId, KvitteriaDbContext dbContext, CancellationToken ct) => {
				ItemAlias? itemAlias = await dbContext.ItemAliases.FirstOrDefaultAsync(ia => ia.Id == aliasId, ct);

				if (itemAlias == null)
					return Results.NotFound($"Item alias with id {aliasId} not found");

				dbContext.ItemAliases.Remove(itemAlias);
				await dbContext.SaveChangesAsync(ct);

				return Results.NoContent();
			});
		}
	}
}
