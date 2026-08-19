using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Receipts;
using Backend.Features.Shared;
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
				                                                          .Include(rl => rl.Meal)
				                                                          .Include(rl => rl.Category)
				                                                          .AsNoTracking()
				                                                          .FirstOrDefaultAsync(rl => rl.Id == id, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {id} not found");

				                return Results.Ok(receiptLine.ToDto());
			                })
			                .WithName("GetReceiptLineById")
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status404NotFound);

			// PATCH /receipt-lines/{id}
			receiptLineGroup.MapPatch("/{id:guid}", async (Guid id, PatchReceiptLineDto patchDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .Include(rl => rl.CanonicalItem)
				                                                          .Include(rl => rl.Meal)
				                                                          .Include(rl => rl.Category)
				                                                          .FirstOrDefaultAsync(rl => rl.Id == id, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {id} not found");

				                if (patchDto.RawName != null)
					                receiptLine.RawName = patchDto.RawName;
				                if (patchDto.Quantity.HasValue)
					                receiptLine.Quantity = patchDto.Quantity.Value;
				                if (patchDto.UnitPrice.HasValue)
					                receiptLine.UnitPrice = patchDto.UnitPrice.Value;
				                if (patchDto.LineTotal.HasValue)
					                receiptLine.LineTotal = patchDto.LineTotal.Value;

				                await dbContext.SaveChangesAsync(ct);
				                return Results.Ok(receiptLine.ToDto());
			                })
			                .WithName("PatchReceiptLine")
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status404NotFound);

			// PUT /receipt-lines/{id}/canonical-item
			receiptLineGroup.MapPut("/{id:guid}/canonical-item", async (Guid id, PutCanonicalItemDto putCanonicalItemDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .FirstOrDefaultAsync(rl => rl.Id == id, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {id} not found");

				                // Skipped when no id was supplied.
				                CanonicalItem? canonicalItem = null;

				                if (putCanonicalItemDto.CanonicalItemId != null)
				                {
					                canonicalItem = await dbContext.CanonicalItems
					                                               .FirstOrDefaultAsync(ci => ci.Id == putCanonicalItemDto.CanonicalItemId, ct);

					                if (canonicalItem == null)
						                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Canonical item with id {putCanonicalItemDto.CanonicalItemId} not found");
				                }

				                receiptLine.CanonicalItemId = canonicalItem?.Id;
				                receiptLine.CanonicalItem = canonicalItem;

				                if (canonicalItem != null && putCanonicalItemDto.CreateOrUpdateAlias)
				                {
					                string normalizedRawName = receiptLine.RawName.NormalizeString();

					                ItemAlias? existingAlias = await dbContext.ItemAliases
					                                          .FirstOrDefaultAsync(ia => ia.RawName == normalizedRawName, ct);

					                if (existingAlias != null)
						                existingAlias.CanonicalItemId = canonicalItem.Id;
					                else
						                dbContext.ItemAliases.Add(new ItemAlias {
							                RawName = normalizedRawName,
							                CanonicalItemId = canonicalItem.Id
						                });
				                }

				                if (!await dbContext.TrySaveChangesAsync(ct))
					                return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"An alias for '{receiptLine.RawName}' already points at another canonical item.");
				                return Results.Ok(receiptLine.ToDto());
			                })
			                .WithName("UpdateReceiptLineCanonicalItem")
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status404NotFound)
			                .ProducesProblem(StatusCodes.Status409Conflict);

			// PUT /receipt-lines/id/category
			receiptLineGroup.MapPut("/{id:guid}/category", async (Guid id, PutReceiptLineCategoryDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .Include(rl => rl.CanonicalItem)
				                                                          .Include(rl => rl.Meal)
				                                                          .FirstOrDefaultAsync(rl => rl.Id == id, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {id} not found");

				                Category? category = null;

				                if (dto.CategoryId != null)
				                {
					                category = await dbContext.Categories.FirstOrDefaultAsync(m => m.Id == dto.CategoryId, ct);

					                if (category == null)
						                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Category with id {dto.CategoryId} not found");
				                }

				                if (dto.SetAsDefault && receiptLine.CanonicalItemId == null)
					                return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"SetAsDefault requires the receipt line to have a canonical item.");

				                receiptLine.CategoryId = category?.Id;
				                receiptLine.Category = category;

				                if (dto.SetAsDefault && receiptLine.CanonicalItem != null)
					                receiptLine.CanonicalItem.CategoryId = category?.Id;

				                await dbContext.SaveChangesAsync(ct);

				                return Results.Ok(receiptLine.ToDto());
			                })
			                .WithName("UpdateReceiptLineCategory")
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status400BadRequest)
			                .ProducesProblem(StatusCodes.Status404NotFound);

			// PUT /receipt-lines/id/meal
			receiptLineGroup.MapPut("/{id:guid}/meal", async (Guid id, PutReceiptLineMealDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .Include(rl => rl.CanonicalItem)
				                                                          .Include(rl => rl.Category)
				                                                          .FirstOrDefaultAsync(rl => rl.Id == id, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {id} not found");

				                Meal? meal = null;

				                if (dto.MealId != null)
				                {
					                meal = await dbContext.Meals.FirstOrDefaultAsync(m => m.Id == dto.MealId, ct);

					                if (meal == null)
						                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Meal with id {dto.MealId} not found");
				                }

				                if (dto.SetAsDefault && receiptLine.CanonicalItemId == null)
					                return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"SetAsDefault requires the receipt line to have a canonical item.");

				                Guid? previousMealId = receiptLine.MealId;

				                receiptLine.MealId = meal?.Id;
				                receiptLine.Meal = meal;

				                if (dto.SetAsDefault && receiptLine.CanonicalItem != null)
					                receiptLine.CanonicalItem.DefaultMealId = meal?.Id;

				                // Coverage is only valid while a line still carries the meal it describes.
				                if (previousMealId != null && previousMealId != receiptLine.MealId)
					                await MealCoverage.ClearOrphanedAsync(dbContext, receiptLine.ReceiptId, previousMealId.Value, id, ct);

				                await dbContext.SaveChangesAsync(ct);

				                return Results.Ok(receiptLine.ToDto());
			                })
			                .WithName("UpdateReceiptLineMeal")
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status400BadRequest)
			                .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}
}
