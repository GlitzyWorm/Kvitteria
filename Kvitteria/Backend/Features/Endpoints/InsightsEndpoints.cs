using Backend.Features.Categories;
using Backend.Features.Insights;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;
using Kvitteria.Contracts.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class InsightsEndpoints
{
	extension(RouteGroupBuilder app)
	{
		public void MapInsightsEndpoints()
		{
			RouteGroupBuilder insights = app.MapGroup("/insights");

			// GET /insights/summary?month=
			insights.MapGet("/summary", async (string? month, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            if (!MonthKey.TryResolve(month, "month", out MonthKey resolved, out IResult monthProblem))
					            return monthProblem;

				            // The previous month is needed for the comparison, so the range covers both.
				            DateTime start = resolved.Previous().RangeUtc().StartUtc;
				            DateTime end = resolved.RangeUtc().EndUtc;

				            List<ReceiptFigure> receipts = await dbContext.Receipts
				                                                          .AsNoTracking()
				                                                          .Where(r => r.PurchaseDate >= start && r.PurchaseDate < end)
				                                                          .Select(r => new ReceiptFigure(r.PurchaseDate, r.GrandTotal, r.PantTotal))
				                                                          .ToListAsync(ct);

				            List<LineFigure> lines = await dbContext.ReceiptLines
				                                                    .AsNoTracking()
				                                                    .Where(l => l.Receipt!.PurchaseDate >= start && l.Receipt.PurchaseDate < end)
				                                                    .Select(l => new LineFigure(l.Receipt!.PurchaseDate, l.LineTotal, l.CategoryId))
				                                                    .ToListAsync(ct);

				            return Results.Ok(SpendingRollup.Summary(resolved, receipts, lines));
			            })
			            .WithName("GetSpendingSummary")
			            .Produces<SpendingSummaryDto>()
			            .ProducesProblem(StatusCodes.Status400BadRequest);

			// GET /insights/trend?endMonth=&months=
			insights.MapGet("/trend", async (
				            string? endMonth,
				            KvitteriaDbContext dbContext,
				            CancellationToken ct,
				            int months = MonthKey.DefaultRun) => {
				            if (!MonthKey.TryValidateRun(endMonth, months, out MonthKey end, out IResult problem))
					            return problem;

				            List<MonthKey> run = end.RunEndingHere(months);

				            // One month before the run, so its first entry has something to compare against.
				            DateTime start = run[0].Previous().RangeUtc().StartUtc;
				            DateTime stop = end.RangeUtc().EndUtc;

				            List<ReceiptFigure> receipts = await dbContext.Receipts
				                                                          .AsNoTracking()
				                                                          .Where(r => r.PurchaseDate >= start && r.PurchaseDate < stop)
				                                                          .Select(r => new ReceiptFigure(r.PurchaseDate, r.GrandTotal, r.PantTotal))
				                                                          .ToListAsync(ct);

				            return Results.Ok(SpendingRollup.Trend(run, receipts));
			            })
			            .WithName("GetSpendingTrend")
			            .Produces<List<SpendingTrendMonthDto>>()
			            .ProducesProblem(StatusCodes.Status400BadRequest);

			// GET /insights/categories?month=&compareTo=&parentId=
			insights.MapGet("/categories", async (
				            string? month,
				            string? compareTo,
				            Guid? parentId,
				            KvitteriaDbContext dbContext,
				            CancellationToken ct) => {
				            if (!MonthKey.TryResolve(month, "month", out MonthKey resolved, out IResult monthProblem))
					            return monthProblem;

				            MonthKey? comparison = null;

				            if (!string.IsNullOrEmpty(compareTo))
				            {
					            if (!MonthKey.TryParse(compareTo, out MonthKey parsed))
						            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"compareTo must be a month written yyyy-MM, for example 2026-03.");

					            comparison = parsed;
				            }

				            CategoryTree tree = await CategoryTree.LoadAsync(dbContext, ct);

				            if (parentId != null)
				            {
					            if (!tree.Contains(parentId.Value))
						            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Category with id {parentId} not found");

					            if (tree.Depth(parentId.Value) != 1)
						            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"parentId must be a top-level category.");
				            }

				            (DateTime currentStart, DateTime currentEnd) = resolved.RangeUtc();
				            (DateTime compareStart, DateTime compareEnd) = (comparison ?? resolved).RangeUtc();

				            List<LineFigure> lines = await dbContext.ReceiptLines
				                                                    .AsNoTracking()
				                                                    .Where(l => (l.Receipt!.PurchaseDate >= currentStart && l.Receipt.PurchaseDate < currentEnd) ||
				                                                                (l.Receipt.PurchaseDate >= compareStart && l.Receipt.PurchaseDate < compareEnd))
				                                                    .Select(l => new LineFigure(l.Receipt!.PurchaseDate, l.LineTotal, l.CategoryId))
				                                                    .ToListAsync(ct);

				            return Results.Ok(CategoryRollup.Breakdown(tree, resolved, comparison, parentId, lines));
			            })
			            .WithName("GetCategoryBreakdown")
			            .Produces<CategoryBreakdownDto>()
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status404NotFound);

			// GET /insights/prices?page=&pageSize=
			insights.MapGet("/prices", async (
				            KvitteriaDbContext dbContext,
				            CancellationToken ct,
				            int pageSize = 20,
				            int page = 1) => {
				            if (!Paging.TryValidate(page, pageSize, out IResult pagingProblem))
					            return pagingProblem;

				            // The same rule as PriceHistory.IsPriced, so an item is listed only if it has a price to show.
				            var query = dbContext.CanonicalItems
				                                 .AsNoTracking()
				                                 .Select(i => new {
					                                 i.Id,
					                                 i.DisplayName,
					                                 PurchaseCount = dbContext.ReceiptLines.Count(l => l.CanonicalItemId == i.Id && l.Quantity > 0 && l.LineTotal > 0)
				                                 })
				                                 .Where(i => i.PurchaseCount > 0);

				            int totalCount = await query.CountAsync(ct);

				            List<PricedItemDto> items = await query
				                                              .OrderByDescending(i => i.PurchaseCount)
				                                              .ThenBy(i => i.DisplayName)
				                                              .Skip((page-1)*pageSize)
				                                              .Take(pageSize)
				                                              .Select(i => new PricedItemDto(i.Id, i.DisplayName, i.PurchaseCount))
				                                              .ToListAsync(ct);

				            return Results.Ok(new PagedResult<PricedItemDto>(items, page, pageSize, totalCount));
			            })
			            .WithName("GetPricedItems")
			            .Produces<PagedResult<PricedItemDto>>()
			            .ProducesProblem(StatusCodes.Status400BadRequest);

			// GET /insights/prices/{itemId}?months=
			insights.MapGet("/prices/{itemId:guid}", async (
				            Guid itemId,
				            KvitteriaDbContext dbContext,
				            CancellationToken ct,
				            int months = MonthKey.DefaultRun) => {
				            if (!MonthKey.TryValidateRun(null, months, out MonthKey end, out IResult problem))
					            return problem;

				            CanonicalItem? item = await dbContext.CanonicalItems
				                                                 .AsNoTracking()
				                                                 .FirstOrDefaultAsync(i => i.Id == itemId, ct);

				            if (item == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Canonical item with id {itemId} not found");

				            List<MonthKey> run = end.RunEndingHere(months);
				            DateTime start = run[0].RangeUtc().StartUtc;
				            DateTime stop = end.RangeUtc().EndUtc;

				            List<PricedLine> lines = await dbContext.ReceiptLines
				                                                    .AsNoTracking()
				                                                    .Where(l => l.CanonicalItemId == itemId && l.Receipt!.PurchaseDate >= start && l.Receipt.PurchaseDate < stop)
				                                                    .Select(l => new PricedLine(l.Receipt!.PurchaseDate, l.Quantity, l.LineTotal))
				                                                    .ToListAsync(ct);

				            return Results.Ok(PriceHistory.Build(item.Id, item.DisplayName, run, lines));
			            })
			            .WithName("GetPriceHistory")
			            .Produces<PriceHistoryDto>()
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status404NotFound);

			// GET /insights/meal-coverage?endMonth=&months=
			insights.MapGet("/meal-coverage", async (
				            string? endMonth,
				            KvitteriaDbContext dbContext,
				            CancellationToken ct,
				            int months = MonthKey.DefaultRun) => {
				            if (!MonthKey.TryValidateRun(endMonth, months, out MonthKey end, out IResult problem))
					            return problem;

				            List<TrackedMeal> meals = await dbContext.Meals
				                                                     .AsNoTracking()
				                                                     .Where(m => m.TracksDayCoverage)
				                                                     .OrderBy(m => m.SortOrder)
				                                                     .ThenBy(m => m.DisplayName)
				                                                     .Select(m => new TrackedMeal(m.Id, m.DisplayName))
				                                                     .ToListAsync(ct);

				            List<MonthKey> run = end.RunEndingHere(months);
				            DateTime start = run[0].RangeUtc().StartUtc;
				            DateTime stop = end.RangeUtc().EndUtc;

				            List<MealLineFigure> lines = await dbContext.ReceiptLines
				                                                        .AsNoTracking()
				                                                        .Where(l => l.Meal!.TracksDayCoverage && l.Receipt!.PurchaseDate >= start && l.Receipt.PurchaseDate < stop)
				                                                        .Select(l => new MealLineFigure(l.Receipt!.PurchaseDate, l.MealId!.Value, l.LineTotal))
				                                                        .ToListAsync(ct);

				            List<CoverageFigure> coverage = await dbContext.ReceiptMealCoverages
				                                                           .AsNoTracking()
				                                                           .Where(c => c.Meal!.TracksDayCoverage && c.Receipt!.PurchaseDate >= start && c.Receipt.PurchaseDate < stop)
				                                                           .Select(c => new CoverageFigure(c.Receipt!.PurchaseDate, c.MealId, c.DaysCovered))
				                                                           .ToListAsync(ct);

				            return Results.Ok(MealCoverageStats.Build(meals, run, lines, coverage));
			            })
			            .WithName("GetMealCoverageStats")
			            .Produces<List<MealCoverageStatsDto>>()
			            .ProducesProblem(StatusCodes.Status400BadRequest);
		}
	}
}
