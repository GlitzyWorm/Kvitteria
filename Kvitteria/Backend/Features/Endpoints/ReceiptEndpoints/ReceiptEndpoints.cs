using Backend.Features.Endpoints.ReceiptEndpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints.ReceiptEndpoints;

public static class ReceiptEndpoints
{
	public static void MapReceiptEndpoints(this RouteGroupBuilder app)
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

			            ReceiptStatus receiptStatus;
			            try
			            {
				            receiptStatus = ReceiptStatusExtension.FromIntToReceiptStatus(status);
			            } catch (Exception e)
			            {
				            return Results.BadRequest(e.Message);
			            }

			            IQueryable<Receipt> query = dbContext.Receipts
			                                                 .Where(r => (merchantId == null || r.MerchantId == merchantId) &&
			                                                             (from == null || r.PurchaseDate >= from) &&
			                                                             (to == null || r.PurchaseDate <= to) &&
			                                                             r.Status == receiptStatus)
			                                                 .AsNoTracking();

			            int totalCount = await query.CountAsync(ct);

			            List<ReceiptDto> receiptDtos = await query
			                                                 .Include(r => r.Merchant)
			                                                 .Include(r => r.ReceiptLines)
			                                                 .OrderByDescending(r => r.PurchaseDate)
			                                                 .Skip((page-1)*pageSize)
			                                                 .Take(pageSize)
			                                                 .Select(r => new ReceiptDto(
				                                                 r.Id,
				                                                 r.Merchant!.Name,
				                                                 r.PurchaseDate,
				                                                 r.GrandTotal,
				                                                 r.PantTotal,
				                                                 r.Reconciled,
				                                                 r.Status,
				                                                 r.ReceiptLines.Select(rl => new ReceiptLineDto(
					                                                 rl.Id, rl.RawName, rl.Quantity, rl.UnitPrice, rl.LineTotal,
					                                                 rl.CanonicalItemId, rl.CanonicalItem == null ? null : rl.CanonicalItem.DisplayName)).ToList()
			                                                 ))
			                                                 .ToListAsync(ct);

			            return Results.Ok(new PagedResult<ReceiptDto>(receiptDtos, page, pageSize, totalCount));
		            })
		            .WithName("GetReceipts")
		            .Produces<PagedResult<ReceiptDto>>();
	}
}