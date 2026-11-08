using Backend.Features.Auth;
using Backend.Features.Endpoints.Extensions;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Receipts;
using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;
using Kvitteria.Contracts.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class ReceiptEndpoints
{

	extension(RouteGroupBuilder app)
	{
		public void MapReceiptEndpoints()
		{
			app.MapReceiptGroupEndpoints();
			app.MapReceiptUploadEndpoints();
			app.MapReceiptLineEndpoints();
			app.MapCanonicalItemEndpoints();
			app.MapItemAliasEndpoints();
			app.MapCategoryEndpoints();
			app.MapMealEndpoints();
			app.MapUserEndpoints();
			app.MapSettingsEndpoints();
			app.MapMailboxEndpoints();
			app.MapPipelineEndpoints();
			app.MapInsightsEndpoints();
			app.MapSystemEndpoints();
		}

		private void MapReceiptGroupEndpoints()
		{
			RouteGroupBuilder receiptGroup = app.MapGroup("/receipts");

			// GET /receipts/?merchantId=&from=&to=&status=&reconciled=&search=&page=&pageSize=
			receiptGroup.MapGet("/", async (
				            Guid? merchantId,
				            DateTime? from,
				            DateTime? to,
				            ReceiptStatus? status,
				            bool? reconciled,
				            string? search,
				            KvitteriaDbContext dbContext,
				            CancellationToken ct,
				            int pageSize = 10,
				            int page = 1) => {
				            if (!Paging.TryValidate(page, pageSize, out IResult pagingProblem))
					            return pagingProblem;

				            if (!ReceiptSearch.TryParse(search, out ReceiptSearchTerm? term, out IResult searchProblem))
					            return searchProblem;

				            IQueryable<Receipt> query = dbContext.Receipts
				                                                 .Where(r => (merchantId == null || r.MerchantId == merchantId) &&
				                                                             (from == null || r.PurchaseDate >= from) &&
				                                                             (to == null || r.PurchaseDate <= to) &&
				                                                             (status == null || r.Status == status) &&
				                                                             (reconciled == null || r.Reconciled == reconciled))
				                                                 .AsNoTracking();

				            if (term != null)
				            {
					            string pattern = term.Pattern;
					            decimal? amountFrom = term.AmountFrom;
					            decimal? amountTo = term.AmountTo;

					            // Any() on the lines keeps a receipt with several matching lines to one row, and leaves
					            // its other lines for the Includes below.
					            query = query.Where(r => EF.Functions.ILike(r.Merchant!.Name, pattern, ReceiptSearch.EscapeChar) ||
					                                     (r.AppUser != null && EF.Functions.ILike(r.AppUser.DisplayName, pattern, ReceiptSearch.EscapeChar)) ||
					                                     r.ReceiptLines.Any(l => EF.Functions.ILike(l.RawName, pattern, ReceiptSearch.EscapeChar) ||
					                                                             (l.CanonicalItem != null && EF.Functions.ILike(l.CanonicalItem.DisplayName, pattern, ReceiptSearch.EscapeChar))) ||
					                                     (amountFrom != null && r.GrandTotal >= amountFrom && r.GrandTotal < amountTo));
				            }

				            int totalCount = await query.CountAsync(ct);

				            // AsSplitQuery: two collection includes in one statement multiply rows against each other.
				            List<ReceiptDto> receiptDtos = await query
				                                                 .OrderByDescending(r => r.PurchaseDate)
				                                                 .Skip((page-1)*pageSize)
				                                                 .Take(pageSize)
				                                                 .Include(r => r.Merchant)
				                                                 .Include(r => r.AppUser)
				                                                 .Include(r => r.ReceiptLines)
				                                                 .ThenInclude(rl => rl.CanonicalItem)
				                                                 .Include(r => r.ReceiptLines)
				                                                 .ThenInclude(rl => rl.Meal)
				                                                 .Include(r => r.ReceiptLines)
				                                                 .ThenInclude(rl => rl.Category)
				                                                 .Include(r => r.MealCoverage)
				                                                 .ThenInclude(c => c.Meal)
				                                                 .AsSplitQuery()
				                                                 .Select(r => r.ToDto())
				                                                 .ToListAsync(ct);

				            return Results.Ok(new PagedResult<ReceiptDto>(receiptDtos, page, pageSize, totalCount));
			            })
			            .WithName("GetReceipts")
			            .Produces<PagedResult<ReceiptDto>>()
			            .ProducesProblem(StatusCodes.Status400BadRequest);

			// GET /receipts/{id}
			receiptGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            Receipt? receipt = await dbContext.Receipts
				                                              .Include(r => r.Merchant)
				                                              .Include(r => r.AppUser)
				                                              .Include(r => r.ReceiptLines)
				                                              .ThenInclude(rl => rl.CanonicalItem)
				                                              .Include(r => r.ReceiptLines)
				                                              .ThenInclude(rl => rl.Meal)
				                                              .Include(r => r.ReceiptLines)
				                                              .ThenInclude(rl => rl.Category)
				                                              .Include(r => r.MealCoverage)
				                                              .ThenInclude(c => c.Meal)
				                                              .AsNoTracking()
				                                              .AsSplitQuery()
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

				            // Only the coverage rows are needed here; assigned meals are projected separately below.
				            Receipt? receipt = await dbContext.Receipts
				                                              .Include(r => r.MealCoverage)
				                                              .FirstOrDefaultAsync(r => r.Id == id, ct);

				            if (receipt == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            if (!MealCoverage.TryValidate(dto.Coverage, out IResult coverageProblem))
					            return coverageProblem;

				            List<Guid> mealIds = dto.Coverage.Select(c => c.MealId).ToList();

				            List<Meal> meals = await dbContext.Meals
				                                              .Where(m => mealIds.Contains(m.Id))
				                                              .AsNoTracking()
				                                              .ToListAsync(ct);

				            List<Guid> missingIds = mealIds.Except(meals.Select(m => m.Id)).ToList();

				            if (missingIds.Count != 0)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Meals not found: {string.Join(", ", missingIds)}");

				            // Only meals that opt into day tracking can carry coverage.
				            List<string> unsupported = meals.Where(m => !m.TracksDayCoverage)
				                                            .Select(m => m.DisplayName)
				                                            .ToList();

				            if (unsupported.Count != 0)
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"Day coverage is not supported for: {string.Join(", ", unsupported)}.");

				            // Coverage must describe meals the receipt actually has lines for.
				            HashSet<Guid> assignedMealIds = await dbContext.ReceiptLines
				                                                           .Where(rl => rl.ReceiptId == id && rl.MealId != null)
				                                                           .Select(rl => rl.MealId!.Value)
				                                                           .Distinct()
				                                                           .ToHashSetAsync(ct);

				            List<string> unassigned = meals.Where(m => !assignedMealIds.Contains(m.Id))
				                                           .Select(m => m.DisplayName)
				                                           .ToList();

				            if (unassigned.Count != 0)
					            return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"No receipt line on this receipt is assigned to: {string.Join(", ", unassigned)}.");

				            Dictionary<Guid, int> desired = dto.Coverage.ToDictionary(c => c.MealId, c => c.DaysCovered);

				            MealCoverage.Apply(dbContext, MealCoverage.Diff(id, receipt.MealCoverage, desired));

				            await dbContext.SaveChangesAsync(ct);

				            List<ReceiptMealCoverageDto> result = meals
				                                                  .OrderBy(m => m.SortOrder)
				                                                  .ThenBy(m => m.DisplayName)
				                                                  .Select(m => new ReceiptMealCoverageDto(m.Id, m.DisplayName, desired[m.Id]))
				                                                  .ToList();

				            return Results.Ok(result);
			            })
			            .WithName("PutReceiptMealCoverage")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.CategorizeLines))
			            .Produces<List<ReceiptMealCoverageDto>>()
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status404NotFound)
			            .ProducesProblem(StatusCodes.Status409Conflict);

		}
	}
}