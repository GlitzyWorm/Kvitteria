using Backend.Features.Auth;
using Backend.Features.Endpoints.Extensions;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Receipts;
using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;
using Kvitteria.Contracts.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Endpoints;

static class ReceiptLineEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapReceiptLineEndpoints()
		{
			RouteGroupBuilder receiptLineGroup = app.MapGroup("/receipt-lines");

			// GET /receipt-lines/unassigned?page=&pageSize=
			receiptLineGroup.MapGet("/unassigned", async (KvitteriaDbContext dbContext, CancellationToken ct, int pageSize = 20, int page = 1) => {
				                if (!Paging.TryValidate(page, pageSize, out IResult pagingProblem))
					                return pagingProblem;

				                IQueryable<ReceiptLine> query = dbContext.ReceiptLines
				                                                         .Where(rl => rl.CanonicalItemId == null)
				                                                         .AsNoTracking();

				                int totalCount = await query.CountAsync(ct);

				                // Lines have no printed position, so Id only makes the paging stable.
				                List<UnassignedLineDto> items = await query
				                                                      .OrderByDescending(rl => rl.Receipt!.PurchaseDate)
				                                                      .ThenBy(rl => rl.ReceiptId)
				                                                      .ThenBy(rl => rl.Id)
				                                                      .Skip((page - 1) * pageSize)
				                                                      .Take(pageSize)
				                                                      .Select(rl => new UnassignedLineDto(
					                                                      rl.Id,
					                                                      rl.RawName,
					                                                      rl.Quantity,
					                                                      rl.UnitPrice,
					                                                      rl.LineTotal,
					                                                      rl.ReceiptId,
					                                                      rl.Receipt!.Merchant!.Name,
					                                                      rl.Receipt.PurchaseDate,
					                                                      rl.Receipt.AppUserId,
					                                                      rl.Receipt.AppUser != null ? rl.Receipt.AppUser.DisplayName : null))
				                                                      .ToListAsync(ct);

				                return Results.Ok(new PagedResult<UnassignedLineDto>(items, page, pageSize, totalCount));
			                })
			                .WithName("GetUnassignedReceiptLines")
			                .Produces<PagedResult<UnassignedLineDto>>()
			                .ProducesProblem(StatusCodes.Status400BadRequest);

			// GET /receipt-lines/unassigned/count
			receiptLineGroup.MapGet("/unassigned/count", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				                int count = await dbContext.ReceiptLines.CountAsync(rl => rl.CanonicalItemId == null, ct);
				                return Results.Ok(new UnassignedLineCountDto(count));
			                })
			                .WithName("GetUnassignedReceiptLineCount")
			                .Produces<UnassignedLineCountDto>();

			// POST /receipt-lines/{id}/sort
			receiptLineGroup.MapPost("/{id:guid}/sort", async (Guid id, SortReceiptLineDto dto, KvitteriaDbContext dbContext, CurrentUserService currentUserService, HttpContext http, CancellationToken ct) => {
				                if (!LineSorting.TryValidate(dto, out IResult validationProblem))
					                return validationProblem;

				                // Depends on the body, so it cannot be a route policy. Checked before any database work.
				                if ((dto.NewItemName != null || dto.SetAsDefaults) && !await currentUserService.HasAsync(http.User, Permission.ManageTaxonomy, ct))
					                return Results.Problem(statusCode:StatusCodes.Status403Forbidden, detail:"Creating a canonical item or writing its defaults requires the ManageTaxonomy permission.");

				                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(ct);

				                ReceiptLine? receiptLine = await dbContext.ReceiptLines.FirstOrDefaultAsync(rl => rl.Id == id, ct);

				                if (receiptLine == null)
					                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt line with id {id} not found");

				                if (receiptLine.CanonicalItemId != null)
					                return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:"The receipt line already has a canonical item; it was sorted since it was loaded.");

				                Category? category = null;

				                if (dto.CategoryId != null)
				                {
					                category = await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId, ct);

					                if (category == null)
						                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Category with id {dto.CategoryId} not found");
				                }

				                Meal? meal = null;

				                if (dto.MealId != null)
				                {
					                meal = await dbContext.Meals.FirstOrDefaultAsync(m => m.Id == dto.MealId, ct);

					                if (meal == null)
						                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Meal with id {dto.MealId} not found");
				                }

				                CanonicalItem? canonicalItem;
				                string? newItemName = dto.NewItemName?.Trim();

				                if (newItemName != null)
				                {
					                canonicalItem = new CanonicalItem { DisplayName = newItemName };
					                dbContext.CanonicalItems.Add(canonicalItem);
				                }
				                else
				                {
					                canonicalItem = await dbContext.CanonicalItems.FirstOrDefaultAsync(ci => ci.Id == dto.CanonicalItemId, ct);

					                if (canonicalItem == null)
						                return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Canonical item with id {dto.CanonicalItemId} not found");
				                }

				                Guid? previousMealId = receiptLine.MealId;

				                receiptLine.CanonicalItem = canonicalItem;
				                receiptLine.CategoryId = category?.Id;
				                receiptLine.Category = category;
				                receiptLine.MealId = meal?.Id;
				                receiptLine.Meal = meal;

				                if (dto.SetAsDefaults)
				                {
					                canonicalItem.CategoryId = category?.Id;
					                canonicalItem.DefaultMealId = meal?.Id;
				                }

				                // Executes immediately; the uncommitted transaction rolls it back with everything else.
				                if (previousMealId != null && previousMealId != receiptLine.MealId)
					                await MealCoverage.ClearOrphanedAsync(dbContext, receiptLine.ReceiptId, previousMealId.Value, id, ct);

				                // The item must exist before an alias can point at it, and a clash on its name belongs here.
				                if (!await dbContext.TrySaveChangesAsync(ct))
					                return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A canonical item named '{canonicalItem.DisplayName}' already exists.");

				                int otherLinesAssigned = 0;

				                if (dto.RememberAlias)
				                {
					                string normalizedRawName = receiptLine.RawName.NormalizeString();

					                await ItemAliases.UpsertAsync(dbContext, normalizedRawName, canonicalItem.Id, ct);

					                // Matched in memory so the match uses the same normalization that wrote the alias.
					                List<ReceiptLine> waiting = await dbContext.ReceiptLines
					                                                           .Where(rl => rl.CanonicalItemId == null && rl.Id != id)
					                                                           .ToListAsync(ct);

					                foreach (ReceiptLine match in AliasLearning.Matching(waiting, normalizedRawName))
					                {
						                match.CanonicalItemId = canonicalItem.Id;
						                AliasLearning.FillGaps(match, category?.Id, meal?.Id);
						                otherLinesAssigned++;
					                }

					                if (!await dbContext.TrySaveChangesAsync(ct))
						                return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"An alias for '{receiptLine.RawName}' was created by someone else at the same moment.");
				                }

				                int remainingCount = await dbContext.ReceiptLines.CountAsync(rl => rl.CanonicalItemId == null, ct);

				                await transaction.CommitAsync(ct);

				                return Results.Ok(new SortReceiptLineResultDto(receiptLine.ToDto(), otherLinesAssigned, remainingCount));
			                })
			                .WithName("SortReceiptLine")
			                .RequireAuthorization(PermissionPolicies.Name(Permission.CategorizeLines))
			                .Produces<SortReceiptLineResultDto>()
			                .ProducesProblem(StatusCodes.Status400BadRequest)
			                .ProducesProblem(StatusCodes.Status403Forbidden)
			                .ProducesProblem(StatusCodes.Status404NotFound)
			                .ProducesProblem(StatusCodes.Status409Conflict);

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
			                .RequireAuthorization(PermissionPolicies.Name(Permission.EditReceipts))
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
					                await ItemAliases.UpsertAsync(dbContext, receiptLine.RawName.NormalizeString(), canonicalItem.Id, ct);
				                }

				                if (!await dbContext.TrySaveChangesAsync(ct))
					                return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"An alias for '{receiptLine.RawName}' already points at another canonical item.");
				                return Results.Ok(receiptLine.ToDto());
			                })
			                .WithName("UpdateReceiptLineCanonicalItem")
			                .RequireAuthorization(PermissionPolicies.Name(Permission.CategorizeLines))
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status404NotFound)
			                .ProducesProblem(StatusCodes.Status409Conflict);

			// PUT /receipt-lines/id/category
			receiptLineGroup.MapPut("/{id:guid}/category", async (Guid id, PutReceiptLineCategoryDto dto, KvitteriaDbContext dbContext, CurrentUserService currentUserService, HttpContext http, CancellationToken ct) => {
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

				                if (dto.SetAsDefault && !await currentUserService.HasAsync(http.User, Permission.ManageTaxonomy, ct))
					                return Results.Problem(statusCode:StatusCodes.Status403Forbidden, detail:"SetAsDefault changes the canonical item's defaults, which requires the ManageTaxonomy permission.");

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
			                .RequireAuthorization(PermissionPolicies.Name(Permission.CategorizeLines))
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status400BadRequest)
			                .ProducesProblem(StatusCodes.Status403Forbidden)
			                .ProducesProblem(StatusCodes.Status404NotFound);

			// PUT /receipt-lines/id/meal
			receiptLineGroup.MapPut("/{id:guid}/meal", async (Guid id, PutReceiptLineMealDto dto, KvitteriaDbContext dbContext, CurrentUserService currentUserService, HttpContext http, CancellationToken ct) => {
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

				                if (dto.SetAsDefault && !await currentUserService.HasAsync(http.User, Permission.ManageTaxonomy, ct))
					                return Results.Problem(statusCode:StatusCodes.Status403Forbidden, detail:"SetAsDefault changes the canonical item's defaults, which requires the ManageTaxonomy permission.");

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
			                .RequireAuthorization(PermissionPolicies.Name(Permission.CategorizeLines))
			                .Produces<ReceiptLineDto>()
			                .ProducesProblem(StatusCodes.Status400BadRequest)
			                .ProducesProblem(StatusCodes.Status403Forbidden)
			                .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}
}
