using System.Linq.Expressions;
using Backend.Features.Auth;
using Backend.Features.Extraction;
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

				            decimal tolerance = await ReceiptReconciliation.ReadToleranceAsync(dbContext, ct);

				            IQueryable<Receipt> query = dbContext.Receipts
				                                                 .Where(r => (merchantId == null || r.MerchantId == merchantId) &&
				                                                             (from == null || r.PurchaseDate >= from) &&
				                                                             (to == null || r.PurchaseDate <= to) &&
				                                                             (status == null || r.Status == status))
				                                                 .AsNoTracking();

				            // false is "needs attention": unreconciled and not accepted. true is everything else.
				            if (reconciled == true)
					            query = query.Where(Not(ReceiptReconciliation.NeedsAttention(tolerance)));
				            else if (reconciled == false)
					            query = query.Where(ReceiptReconciliation.NeedsAttention(tolerance));

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
				                                                 .Include(r => r.AcceptedBy)
				                                                 .Include(r => r.ReceiptLines)
				                                                 .ThenInclude(rl => rl.CanonicalItem)
				                                                 .Include(r => r.ReceiptLines)
				                                                 .ThenInclude(rl => rl.Meal)
				                                                 .Include(r => r.ReceiptLines)
				                                                 .ThenInclude(rl => rl.Category)
				                                                 .Include(r => r.MealCoverage)
				                                                 .ThenInclude(c => c.Meal)
				                                                 .AsSplitQuery()
				                                                 .Select(r => r.ToDto(tolerance))
				                                                 .ToListAsync(ct);

				            return Results.Ok(new PagedResult<ReceiptDto>(receiptDtos, page, pageSize, totalCount));
			            })
			            .WithName("GetReceipts")
			            .Produces<PagedResult<ReceiptDto>>()
			            .ProducesProblem(StatusCodes.Status400BadRequest);

			// GET /receipts/{id}
			receiptGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            ReceiptDto? dto = await LoadDtoAsync(dbContext, id, ct);

				            return dto == null
					            ? Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found")
					            : Results.Ok(dto);
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

			// PATCH /receipts/{id}
			receiptGroup.MapPatch("/{id:guid}", async (Guid id, PatchReceiptDto patchDto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            Receipt? receipt = await dbContext.Receipts.FirstOrDefaultAsync(r => r.Id == id, ct);

				            if (receipt == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            // No sign validation: a return receipt is negative, and nothing else validates totals.
				            if (patchDto.GrandTotal.HasValue)
					            receipt.GrandTotal = patchDto.GrandTotal.Value;
				            if (patchDto.PantTotal.HasValue)
					            receipt.PantTotal = patchDto.PantTotal.Value;

				            await dbContext.SaveChangesAsync(ct);

				            return Results.Ok(await LoadDtoAsync(dbContext, id, ct));
			            })
			            .WithName("PatchReceipt")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.EditReceipts))
			            .Produces<ReceiptDto>()
			            .ProducesProblem(StatusCodes.Status404NotFound);

			// POST /receipts/{id}/lines
			receiptGroup.MapPost("/{id:guid}/lines", async (Guid id, CreateReceiptLineDto dto, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            string rawName = dto.RawName?.Trim() ?? "";

				            if (rawName.Length == 0 || rawName.Length > KvitteriaDbContext.RawNameMaxLength)
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"The name is required and at most {KvitteriaDbContext.RawNameMaxLength} characters.");

				            if (!await dbContext.Receipts.AnyAsync(r => r.Id == id, ct))
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            var line = new ReceiptLine {
					            ReceiptId = id,
					            RawName = rawName,
					            Quantity = dto.Quantity,
					            UnitPrice = dto.UnitPrice,
					            LineTotal = dto.LineTotal
				            };

				            await AliasLinking.LinkAsync(dbContext, [line], ct);

				            dbContext.ReceiptLines.Add(line);
				            await dbContext.SaveChangesAsync(ct);

				            ReceiptLine created = await dbContext.ReceiptLines
				                                                 .Include(rl => rl.CanonicalItem)
				                                                 .Include(rl => rl.Meal)
				                                                 .Include(rl => rl.Category)
				                                                 .AsNoTracking()
				                                                 .FirstAsync(rl => rl.Id == line.Id, ct);

				            return Results.Created($"/api/receipt-lines/{created.Id}", created.ToDto());
			            })
			            .WithName("CreateReceiptLine")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.EditReceipts))
			            .Produces<ReceiptLineDto>(StatusCodes.Status201Created)
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status404NotFound);

			// POST /receipts/{id}/accept-difference
			receiptGroup.MapPost("/{id:guid}/accept-difference", async (
				            Guid id,
				            AcceptDifferenceDto dto,
				            KvitteriaDbContext dbContext,
				            CurrentUserService currentUserService,
				            HttpContext http,
				            CancellationToken ct) => {
				            string? note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();

				            if (note != null && note.Length > KvitteriaDbContext.AcceptedNoteLength)
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"The note is at most {KvitteriaDbContext.AcceptedNoteLength} characters.");

				            Receipt? receipt = await dbContext.Receipts
				                                              .Include(r => r.ReceiptLines)
				                                              .FirstOrDefaultAsync(r => r.Id == id, ct);

				            if (receipt == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            decimal tolerance = await ReceiptReconciliation.ReadToleranceAsync(dbContext, ct);
				            decimal delta = ReceiptReconciliation.Delta(receipt);

				            if (Reconciliation.IsReconciled(delta, tolerance))
					            return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:"The receipt already reconciles, so there is no difference to accept.");

				            if (dto.Difference != delta)
					            return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"The difference is now {delta:0.00} kr, not {dto.Difference:0.00} kr. The receipt changed since it was loaded.");

				            CurrentUser? user = await currentUserService.ResolveAsync(http.User, ct);

				            receipt.AcceptedDifference = delta;
				            receipt.AcceptedAt = DateTime.UtcNow;
				            receipt.AcceptedById = user?.Id;
				            receipt.AcceptedNote = note;

				            await dbContext.SaveChangesAsync(ct);

				            return Results.Ok(await LoadDtoAsync(dbContext, id, ct));
			            })
			            .WithName("AcceptReceiptDifference")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.EditReceipts))
			            .Produces<ReceiptDto>()
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status404NotFound)
			            .ProducesProblem(StatusCodes.Status409Conflict);

			// DELETE /receipts/{id}/accept-difference
			receiptGroup.MapDelete("/{id:guid}/accept-difference", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            // 204 whether or not there was an acceptance to clear.
				            await dbContext.Receipts
				                           .Where(r => r.Id == id)
				                           .ExecuteUpdateAsync(s => s
				                                                    .SetProperty(r => r.AcceptedDifference, (decimal?)null)
				                                                    .SetProperty(r => r.AcceptedAt, (DateTime?)null)
				                                                    .SetProperty(r => r.AcceptedById, (Guid?)null)
				                                                    .SetProperty(r => r.AcceptedNote, (string?)null), ct);

				            return Results.NoContent();
			            })
			            .WithName("WithdrawReceiptDifferenceAcceptance")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.EditReceipts))
			            .Produces(StatusCodes.Status204NoContent);
		}
	}

	private static Expression<Func<Receipt, bool>> Not(Expression<Func<Receipt, bool>> predicate) =>
		Expression.Lambda<Func<Receipt, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

	private static async Task<ReceiptDto?> LoadDtoAsync(KvitteriaDbContext dbContext, Guid id, CancellationToken ct)
	{
			decimal tolerance = await ReceiptReconciliation.ReadToleranceAsync(dbContext, ct);

			Receipt? receipt = await dbContext.Receipts
			                                  .Include(r => r.Merchant)
			                                  .Include(r => r.AppUser)
			                                  .Include(r => r.AcceptedBy)
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

		return receipt?.ToDto(tolerance);
	}
}