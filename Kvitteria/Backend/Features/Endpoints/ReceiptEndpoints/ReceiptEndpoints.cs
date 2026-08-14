using Backend.Features.Endpoints.ReceiptEndpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints.ReceiptEndpoints;

public static class ReceiptEndpoints
{
	extension(RouteGroupBuilder app)
	{
		public void MapReceiptEndpoints()
		{
			app.MapReceiptGroupEndpoints();
			app.MapReceiptLineEndpoints();
			app.MapCanonicalItemEndpoints();
			app.MapItemAliasEndpoints();
		}

		private void MapReceiptGroupEndpoints()
		{
			RouteGroupBuilder receiptGroup = app.MapGroup("/receipts");

			// GET /receipts/?merchantId=&from=&to=&status=&page=&pageSize=
			receiptGroup.MapGet("/", async (
				            Guid? merchantId,
				            DateTime? from,
				            DateTime? to,
				            int? status,
				            KvitteriaDbContext dbContext,
				            CancellationToken ct,
				            int pageSize = 10,
				            int page = 1) => {
				            if (page < 1 || pageSize < 1 || pageSize > 100)
					            return Results.BadRequest("page must be >= 1 and pageSize must be between 1 and 100");
				            
				            ReceiptStatus? receiptStatus = null;
				            try
				            {
					            if (status != null) 
						            receiptStatus = ReceiptStatusExtension.FromIntToReceiptStatus(status);
				            } catch (Exception e)
				            {
					            return Results.BadRequest(e.Message);
				            }

				            IQueryable<Receipt> query = dbContext.Receipts
				                                                 .Where(r => (merchantId == null || r.MerchantId == merchantId) &&
				                                                             (from == null || r.PurchaseDate >= from) &&
				                                                             (to == null || r.PurchaseDate <= to) &&
				                                                             (status == null || r.Status == receiptStatus))
				                                                 .AsNoTracking();

				            int totalCount = await query.CountAsync(ct);

				            if (totalCount == 0)
					            return Results.NotFound("No receipts found");

				            List<ReceiptDto> receiptDtos = await query
				                                                 .Include(r => r.Merchant)
				                                                 .Include(r => r.ReceiptLines)
				                                                 .OrderByDescending(r => r.PurchaseDate)
				                                                 .Skip((page-1)*pageSize)
				                                                 .Take(pageSize)
				                                                 .Select(r => r.ToDto())
				                                                 .ToListAsync(ct);

				            return Results.Ok(new PagedResult<ReceiptDto>(receiptDtos, page, pageSize, totalCount));
			            })
			            .WithName("GetReceipts")
			            .Produces<PagedResult<ReceiptDto>>();

			// GET /receipts/{id}
			receiptGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            Receipt? receipt = await dbContext.Receipts
				                                              .Include(r => r.Merchant)
				                                              .Include(r => r.ReceiptLines)
				                                              .ThenInclude(rl => rl.CanonicalItem)
				                                              .AsNoTracking()
				                                              .FirstOrDefaultAsync(r => r.Id == id, ct);

				            if (receipt == null)
					            return Results.NotFound($"Receipt with id {id} not found");

				            return Results.Ok(receipt.ToDto());
			            })
			            .WithName("GetReceiptById")
			            .Produces<ReceiptDto>();
		}

		private void MapReceiptLineEndpoints()
		{
			RouteGroupBuilder receiptLineGroup = app.MapGroup("/receipt-lines");

			// GET /receipt-lines/{id}
			receiptLineGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .Include(rl => rl.CanonicalItem)
				                                                          .AsNoTracking()
				                                                          .FirstOrDefaultAsync(rl => rl.Id == id, ct);

				                if (receiptLine == null)
					                return Results.NotFound($"Receipt line with id {id} not found");

				                return Results.Ok(receiptLine.ToDto(receiptLine.CanonicalItem));
			                })
			                .WithName("GetReceiptLineById")
			                .Produces<ReceiptLineDto>();

			// PATCH /receipt-lines/{id}
			receiptLineGroup.MapPatch("/{receiptLineId:guid}", async (Guid receiptLineId, PatchReceiptLineDto patchDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines.Include(receiptLine => receiptLine.CanonicalItem)
				                                                          .FirstOrDefaultAsync(rl => rl.Id == receiptLineId, ct);

				                if (receiptLine == null)
					                return Results.NotFound($"Receipt line with id {receiptLineId} not found");

				                if (patchDto.RawName != null)
					                receiptLine.RawName = patchDto.RawName;
				                if (patchDto.Quantity.HasValue)
					                receiptLine.Quantity = patchDto.Quantity.Value;
				                if (patchDto.UnitPrice.HasValue)
					                receiptLine.UnitPrice = patchDto.UnitPrice.Value;
				                if (patchDto.LineTotal.HasValue)
					                receiptLine.LineTotal = patchDto.LineTotal.Value;

				                await dbContext.SaveChangesAsync(ct);
				                return Results.Ok(receiptLine.ToDto(receiptLine.CanonicalItem));
			                })
			                .WithName("PatchReceiptLine")
			                .Produces<ReceiptLineDto>();

			// PUT /receipt-lines/{id}/canonical-item
			receiptLineGroup.MapPut("/{receiptLineId:guid}/canonical-item", async (Guid receiptLineId, PutCanonicalItemDto putCanonicalItemDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .FirstOrDefaultAsync(rl => rl.Id == receiptLineId, ct);

				                if (receiptLine == null)
					                return Results.NotFound($"Receipt line with id {receiptLineId} not found");

				                CanonicalItem? canonicalItem = await dbContext.CanonicalItems
				                                                              .FirstOrDefaultAsync(ci => ci.Id == putCanonicalItemDto.CanonicalItemId, ct);

				                if (putCanonicalItemDto.CanonicalItemId != null && canonicalItem == null)
					                return Results.NotFound($"Canonical item with id {putCanonicalItemDto} not found");

				                receiptLine.CanonicalItemId = canonicalItem?.Id;

				                try
				                {
					                if (canonicalItem != null && putCanonicalItemDto.CreateOrUpdateAlias)
					                {
						                ItemAlias? existingAlias = await dbContext.ItemAliases
						                                                          .FirstOrDefaultAsync(ia => ia.RawName == receiptLine.RawName, ct);

						                if (existingAlias != null)
						                {
							                existingAlias.CanonicalItemId = canonicalItem?.Id ?? throw new InvalidOperationException("Canonical item must not be null when updating alias");
						                }
						                else
						                {
							                var newAlias = new ItemAlias {
								                RawName = receiptLine.RawName,
								                CanonicalItemId = canonicalItem?.Id ?? throw new InvalidOperationException("Canonical item must not be null when creating alias")
							                };
							                dbContext.ItemAliases.Add(newAlias);
						                }

					                }
				                } catch (InvalidOperationException e)
				                {
					                return Results.BadRequest(e.Message);
				                }

				                await dbContext.SaveChangesAsync(ct);
				                return Results.Ok(receiptLine.ToDto(canonicalItem));
			                })
			                .WithName("UpdateReceiptLineCanonicalItem")
			                .Produces<ReceiptLineDto>();
		}

		private void MapCanonicalItemEndpoints()
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
			                  .Produces<PagedResult<ReceiptDto>>();

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

		private void MapItemAliasEndpoints()
		{
			RouteGroupBuilder aliasGroup = app.MapGroup("/aliases");

			// GET /aliases?search=&canonicalId&page=&pageSize=
			aliasGroup.MapGet("/", async (string? search, Guid? canonicalItemId, KvitteriaDbContext dbContext, CancellationToken ct, int pageSize = 10, int page = 1) => {
				          if (page < 1 || pageSize < 1 || pageSize > 100)
					          return Results.BadRequest("page must be >= 1 and pageSize must be between 1 and 100");

				          IQueryable<ItemAlias> query = dbContext.ItemAliases
				                                                 .Include(ia => ia.CanonicalItem)
				                                                 .Where(ia => (search == null || ia.RawName.Contains(search) && (canonicalItemId == null || ia.CanonicalItemId == canonicalItemId)))
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