using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
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
			receiptLineGroup.MapPatch("/{receiptLineId:guid}", async (Guid receiptLineId, PatchReceiptLineDto patchDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .Include(rl => rl.CanonicalItem)
				                                                          .Include(rl => rl.Meal)
				                                                          .Include(rl => rl.Category)
				                                                          .FirstOrDefaultAsync(rl => rl.Id == receiptLineId, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {receiptLineId} not found");

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
			receiptLineGroup.MapPut("/{receiptLineId:guid}/canonical-item", async (Guid receiptLineId, PutCanonicalItemDto putCanonicalItemDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .FirstOrDefaultAsync(rl => rl.Id == receiptLineId, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {receiptLineId} not found");

				                // Only look the item up when one was actually requested; a null id used to issue a
				                // guaranteed-empty query before the null check below.
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

				                await dbContext.SaveChangesAsync(ct);
				                return Results.Ok(receiptLine.ToDto());
			                })
			                .WithName("UpdateReceiptLineCanonicalItem")
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status404NotFound);

			// PUT /receipt-lines/id/category
			receiptLineGroup.MapPut("/{receiptLineId:guid}/category", async (Guid receiptLineId, PutReceiptLineCategoryDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .Include(rl => rl.CanonicalItem)
				                                                          .Include(rl => rl.Meal)
				                                                          .FirstOrDefaultAsync(rl => rl.Id == receiptLineId, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {receiptLineId} not found");

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
			receiptLineGroup.MapPut("/{receiptLineId:guid}/meal", async (Guid receiptLineId, PutReceiptLineMealDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				                ReceiptLine? receiptLine = await dbContext.ReceiptLines
				                                                          .Include(rl => rl.CanonicalItem)
				                                                          .Include(rl => rl.Category)
				                                                          .FirstOrDefaultAsync(rl => rl.Id == receiptLineId, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {receiptLineId} not found");

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
					                await ClearOrphanedCoverage(receiptLine.ReceiptId, previousMealId.Value, receiptLineId, dbContext, ct);

				                await dbContext.SaveChangesAsync(ct);

				                return Results.Ok(receiptLine.ToDto());
			                })
			                .WithName("UpdateReceiptLineMeal")
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status400BadRequest)
			                .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}

	/// <summary>Removes the receipt's coverage row for a meal once no line on that receipt carries it.</summary>
	private static async Task ClearOrphanedCoverage(Guid receiptId, Guid mealId, Guid excludedReceiptLineId, KvitteriaDbContext dbContext, CancellationToken ct)
	{
		// The current line's new MealId is not saved yet, so it is excluded by id rather than by value.
		bool stillAssigned = await dbContext.ReceiptLines
		                                    .AnyAsync(rl => rl.ReceiptId == receiptId
		                                                    && rl.MealId == mealId
		                                                    && rl.Id != excludedReceiptLineId, ct);

		if (stillAssigned)
			return;

		ReceiptMealCoverage? coverage = await dbContext.ReceiptMealCoverages
		                                               .FirstOrDefaultAsync(c => c.ReceiptId == receiptId && c.MealId == mealId, ct);

		if (coverage != null)
			dbContext.ReceiptMealCoverages.Remove(coverage);
	}
}