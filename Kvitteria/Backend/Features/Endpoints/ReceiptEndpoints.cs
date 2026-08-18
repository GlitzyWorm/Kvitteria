using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

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
			app.MapCategoryEndpoints();
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
				                                              .AsNoTracking()
				                                              .FirstOrDefaultAsync(r => r.Id == id, ct);

				            if (receipt == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            return Results.Ok(receipt.ToDto());
			            })
			            .WithName("GetReceiptById")
			            .Produces<ReceiptDto>()
			            .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}
}
