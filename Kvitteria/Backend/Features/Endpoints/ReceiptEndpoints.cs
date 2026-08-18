using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

public static class ReceiptEndpoints
{
	private const int MinDaysCovered = 0;
	private const int MaxDaysCovered = 31;

	extension(RouteGroupBuilder app)
	{
		public void MapReceiptEndpoints()
		{
			app.MapReceiptGroupEndpoints();
			app.MapReceiptLineEndpoints();
			app.MapCanonicalItemEndpoints();
			app.MapItemAliasEndpoints();
			app.MapCategoryEndpoints();
			app.MapMealEndpoints();
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
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"page must be >= 1 and pageSize must be between 1 and 100");

				            ReceiptStatus? receiptStatus = null;
				            try
				            {
					            if (status != null)
						            receiptStatus = ReceiptStatusExtension.FromIntToReceiptStatus(status);
				            } catch (Exception e)
				            {
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:e.Message);
				            }

				            IQueryable<Receipt> query = dbContext.Receipts
				                                                 .Where(r => (merchantId == null || r.MerchantId == merchantId) &&
				                                                             (from == null || r.PurchaseDate >= from) &&
				                                                             (to == null || r.PurchaseDate <= to) &&
				                                                             (status == null || r.Status == receiptStatus))
				                                                 .AsNoTracking();

				            int totalCount = await query.CountAsync(ct);

				            if (totalCount == 0)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:"No receipts found");

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
			            .Produces<PagedResult<ReceiptDto>>()
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status404NotFound);

			// GET /receipts/{id}
			receiptGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            Receipt? receipt = await dbContext.Receipts
				                                              .Include(r => r.Merchant)
				                                              .Include(r => r.ReceiptLines)
				                                              .ThenInclude(rl => rl.CanonicalItem)
				                                              .Include(r => r.ReceiptLines)
				                                              .ThenInclude(rl => rl.Meal)
				                                              .Include(r => r.ReceiptLines)
				                                              .ThenInclude(rl => rl.Category)
				                                              .Include(r => r.MealCoverage)
				                                              .ThenInclude(c => c.Meal)
				                                              .AsNoTracking()
				                                              .FirstOrDefaultAsync(r => r.Id == id, ct);

				            if (receipt == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            return Results.Ok(receipt.ToDto());
			            })
			            .WithName("GetReceiptById")
			            .Produces<ReceiptDto>()
			            .ProducesProblem(StatusCodes.Status404NotFound);

			// PUT /receipts/id/meal-coverage
			receiptGroup.MapPut("/{id:guid}/meal-coverage", async (Guid id, PutReceiptMealCoverageDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            if (dto.Coverage == null)
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"Coverage is required. Send an empty array to clear all coverage.");

				            Receipt? receipt = await dbContext.Receipts
				                                              .Include(r => r.ReceiptLines)
				                                              .Include(r => r.MealCoverage)
				                                              .FirstOrDefaultAsync(r => r.Id == id);

				            if (receipt == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            // Ensure no duplicate meal ids are provided
				            List<Guid> duplicateIds = dto.Coverage.GroupBy(c => c.MealId)
				                                         .Where(g => g.Count() > 1)
				                                         .Select(g => g.Key)
				                                         .ToList();

				            if (duplicateIds.Count != 0)
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"Coverage contains duplicate meal ids: {string.Join(", ", duplicateIds)}");

				            if (dto.Coverage.Any(c => c.DaysCovered is < MinDaysCovered or > MaxDaysCovered))
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"DaysCovered must be between {MinDaysCovered} and {MaxDaysCovered}.");

				            List<Guid> mealIds = dto.Coverage.Select(c => c.MealId).ToList();

				            List<Meal> meals = await dbContext.Meals
				                                              .Where(m => mealIds.Contains(m.Id))
				                                              .AsNoTracking()
				                                              .ToListAsync(ct);

				            // Ensure no unknown meal ids are provided
				            List<Guid> missingIds = mealIds.Except(meals.Select(m => m.Id)).ToList();

				            if (missingIds.Count != 0)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Meals not found: {string.Join(", ", missingIds)}");

				            // Ensure only meals that support day tracking are provided
				            List<string> unsupported = meals.Where(m => !m.TracksDayCoverage)
				                                            .Select(m => m.DisplayName)
				                                            .ToList();

				            if (unsupported.Count != 0)
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"Day coverage is not supported for: {string.Join(", ", unsupported)}.");

				            // Coverage must describe meals the receipt actually has lines for
				            HashSet<Guid> assignedMealIds = receipt.ReceiptLines
				                                                   .Where(rl => rl.MealId != null)
				                                                   .Select(rl => rl.MealId!.Value)
				                                                   .ToHashSet();

				            List<string> unassigned = meals.Where(m => !assignedMealIds.Contains(m.Id))
				                                           .Select(m => m.DisplayName)
				                                           .ToList();

				            if (unassigned.Count != 0)
					            return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"No receipt line on this receipt is assigned to: {string.Join(", ", unassigned)}.");

				            Dictionary<Guid, int> desired = dto.Coverage.ToDictionary(c => c.MealId, c => c.DaysCovered);

				            HashSet<Guid> existingMealIds = receipt.MealCoverage.Select(c => c.MealId).ToHashSet();

				            foreach (ReceiptMealCoverage existing in receipt.MealCoverage.ToList())
				            {
					            if (desired.TryGetValue(existing.MealId, out int days))
						            existing.DaysCovered = days;
					            else
						            dbContext.ReceiptMealCoverages.Remove(existing);
				            }

				            foreach ((Guid mealId, int days) in desired.Where(kvp => !existingMealIds.Contains(kvp.Key)))
					            dbContext.ReceiptMealCoverages.Add(new ReceiptMealCoverage {
						            ReceiptId = id,
						            MealId = mealId,
						            DaysCovered = days
					            });

				            await dbContext.SaveChangesAsync(ct);

				            List<ReceiptMealCoverageDto> result = meals
				                                                  .OrderBy(m => m.SortOrder)
				                                                  .ThenBy(m => m.DisplayName)
				                                                  .Select(m => new ReceiptMealCoverageDto(m.Id, m.DisplayName, desired[m.Id]))
				                                                  .ToList();

				            return Results.Ok(result);
			            })
			            .WithName("PutReceiptMealCoverage")
			            .Produces<List<ReceiptMealCoverageDto>>()
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status404NotFound)
			            .ProducesProblem(StatusCodes.Status409Conflict);

		}
	}
}