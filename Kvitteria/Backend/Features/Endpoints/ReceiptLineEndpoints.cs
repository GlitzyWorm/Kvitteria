using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class ReceiptLineEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapReceiptLineEndpoints()
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
					                string normalizedRawName = receiptLine.RawName.NormalizeString();
					                if (canonicalItem != null && putCanonicalItemDto.CreateOrUpdateAlias)
					                {
						                ItemAlias? existingAlias = await dbContext.ItemAliases
						                                                          .FirstOrDefaultAsync(ia => ia.RawName == normalizedRawName, ct);

						                if (existingAlias != null)
						                {
							                existingAlias.CanonicalItemId = canonicalItem?.Id ?? throw new InvalidOperationException("Canonical item must not be null when updating alias");
						                }
						                else
						                {
							                var newAlias = new ItemAlias {
								                RawName = normalizedRawName,
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
	}
}
