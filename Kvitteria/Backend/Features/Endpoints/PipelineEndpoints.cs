using Backend.Features.Auth;
using Backend.Features.Endpoints.Extensions;
using Backend.Features.Extraction;
using Backend.Features.Mail;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Factory;
using Backend.Features.Persistence.Model;
using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;
using Kvitteria.Contracts.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Endpoints;

static class PipelineEndpoints
{
	extension(RouteGroupBuilder app)
	{
		public void MapPipelineEndpoints()
		{
			RouteGroupBuilder pipelineGroup = app.MapGroup("/pipeline");

			// GET /pipeline/status
			pipelineGroup.MapGet("/status", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				             // Counted from the database, not the channels, so the numbers survive a restart.
				             Dictionary<ReceiptStatus, int> byStage = await dbContext.ReceiptIntakes
				                                                                     .AsNoTracking()
				                                                                     .GroupBy(i => i.Status)
				                                                                     .Select(g => new { Status = g.Key, Count = g.Count() })
				                                                                     .ToDictionaryAsync(g => g.Status, g => g.Count, ct);

				             int unreconciled = await dbContext.Receipts.CountAsync(r => !r.Reconciled, ct);

				             int pollInterval = await dbContext.PipelineSettings
				                                               .AsNoTracking()
				                                               .Select(s => s.ImapPollIntervalSeconds)
				                                               .FirstOrDefaultAsync(ct);

				             DateTime? lastPolledAt = await dbContext.Mailboxes.MaxAsync(m => m.LastPolledAt, ct);

				             (DateTime monthStart, DateTime monthEnd) = DanishTime.MonthRangeUtc(DateTimeOffset.UtcNow);

				             // Receipts from before attribution have no mailbox and so count towards none.
				             Dictionary<Guid, int> receiptsByMailbox = await dbContext.Receipts
				                                                                      .AsNoTracking()
				                                                                      .Where(r => r.MailboxId != null &&
				                                                                                  r.PurchaseDate >= monthStart &&
				                                                                                  r.PurchaseDate < monthEnd)
				                                                                      .GroupBy(r => r.MailboxId!.Value)
				                                                                      .Select(g => new { MailboxId = g.Key, Count = g.Count() })
				                                                                      .ToDictionaryAsync(g => g.MailboxId, g => g.Count, ct);

				             var mailboxRows = await dbContext.Mailboxes
				                                              .AsNoTracking()
				                                              .OrderBy(m => m.Host)
				                                              .ThenBy(m => m.Username)
				                                              .Select(m => new {
					                                              m.Id,
					                                              m.AppUserId,
					                                              MemberDisplayName = m.AppUser!.DisplayName,
					                                              m.Folder,
					                                              m.LastMessageAt
				                                              })
				                                              .ToListAsync(ct);

				             List<MailboxActivityDto> mailboxes = mailboxRows
				                                                  .Select(m => new MailboxActivityDto(
					                                                  m.Id, m.AppUserId, m.MemberDisplayName, m.Folder, m.LastMessageAt,
					                                                  receiptsByMailbox.GetValueOrDefault(m.Id)))
				                                                  .ToList();

				             return Results.Ok(new PipelineStatusDto(
					             byStage.GetValueOrDefault(ReceiptStatus.Queued),
					             byStage.GetValueOrDefault(ReceiptStatus.Extracting),
					             byStage.GetValueOrDefault(ReceiptStatus.Extracted),
					             byStage.GetValueOrDefault(ReceiptStatus.Failed),
					             unreconciled,
					             lastPolledAt,
					             pollInterval,
					             mailboxes));
			             })
			             .WithName("GetPipelineStatus")
			             .Produces<PipelineStatusDto>();

			// GET /pipeline/failures?page=&pageSize=
			pipelineGroup.MapGet("/failures", async (
				             KvitteriaDbContext dbContext,
				             CancellationToken ct,
				             int pageSize = 10,
				             int page = 1) => {
				             if (!Paging.TryValidate(page, pageSize, out IResult pagingProblem))
					             return pagingProblem;

				             IQueryable<ReceiptIntake> query = dbContext.ReceiptIntakes
				                                                        .AsNoTracking()
				                                                        .Where(i => i.Status == ReceiptStatus.Failed);

				             int totalCount = await query.CountAsync(ct);

				             List<ReceiptFailureDto> failures = await query
				                                                      .OrderByDescending(i => i.FailedAt)
				                                                      .ThenBy(i => i.Id)
				                                                      .Skip((page-1)*pageSize)
				                                                      .Take(pageSize)
				                                                      .Select(ReceiptFailureDtoExtension.Projection)
				                                                      .ToListAsync(ct);

				             return Results.Ok(new PagedResult<ReceiptFailureDto>(failures, page, pageSize, totalCount));
			             })
			             .WithName("GetPipelineFailures")
			             .Produces<PagedResult<ReceiptFailureDto>>()
			             .ProducesProblem(StatusCodes.Status400BadRequest);

			// POST /pipeline/failures/{id}/retry
			pipelineGroup.MapPost("/failures/{id:guid}/retry", async (
				             Guid id,
				             KvitteriaDbContext dbContext,
				             ReceiptIntakeStore store,
				             ExtractionQueue queue,
				             CancellationToken ct) => {
				             var previous = await dbContext.ReceiptIntakes
				                                           .AsNoTracking()
				                                           .Where(i => i.Id == id)
				                                           .Select(i => new { i.Status, i.Error, i.FailedAt })
				                                           .FirstOrDefaultAsync(ct);

				             if (previous == null)
					             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Failed mail with id {id} not found");

				             // The conditional update is the lock: of two simultaneous retries only one changes a row.
				             if (previous.Status != ReceiptStatus.Failed || !await store.RequeueFailedAsync(id, ct))
					             return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:"This mail is not failed, so it cannot be retried.");

				             ReceiptRecord? record = await store.LoadAsync(id, ct);

				             // Dismissed between the requeue and the load: nothing is left to retry or restore.
				             if (record == null)
					             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Failed mail with id {id} not found");

				             // Non-blocking on purpose: a blocking enqueue would hold this request open for as long as
				             // extraction takes to free a slot.
				             if (!queue.TryEnqueue(record))
				             {
					             await store.RestoreFailedAsync(id, previous.Error ?? "", previous.FailedAt, CancellationToken.None);

					             return Results.Problem(
						             statusCode:StatusCodes.Status503ServiceUnavailable,
						             detail:"The extraction queue is full. Try again in a moment.");
				             }

				             return Results.Accepted();
			             })
			             .WithName("RetryPipelineFailure")
			             .RequireAuthorization(PermissionPolicies.Name(Permission.EditReceipts))
			             .Produces(StatusCodes.Status202Accepted)
			             .ProducesProblem(StatusCodes.Status404NotFound)
			             .ProducesProblem(StatusCodes.Status409Conflict)
			             .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

			// DELETE /pipeline/failures/{id}
			pipelineGroup.MapDelete("/failures/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				             // An upload's image goes with it, in the same transaction: nothing else will ever point at it.
				             await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(ct);

				             Guid? imageId = await dbContext.ReceiptIntakes
				                                            .Where(i => i.Id == id && i.Status == ReceiptStatus.Failed)
				                                            .Select(i => i.ImageId)
				                                            .FirstOrDefaultAsync(ct);

				             // A conditional delete, so a mail that was retried a moment ago is not removed from under the worker.
				             int deleted = await dbContext.ReceiptIntakes
				                                          .Where(i => i.Id == id && i.Status == ReceiptStatus.Failed)
				                                          .ExecuteDeleteAsync(ct);

				             if (deleted == 1)
				             {
					             if (imageId is { } image)
						             await dbContext.ReceiptImages.Where(img => img.Id == image).ExecuteDeleteAsync(ct);

					             await transaction.CommitAsync(ct);

					             return Results.NoContent();
				             }

				             bool exists = await dbContext.ReceiptIntakes.AnyAsync(i => i.Id == id, ct);

				             return exists
					             ? Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:"This mail is not failed, so it cannot be dismissed.")
					             : Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Failed mail with id {id} not found");
			             })
			             .WithName("DismissPipelineFailure")
			             .RequireAuthorization(PermissionPolicies.Name(Permission.EditReceipts))
			             .Produces(StatusCodes.Status204NoContent)
			             .ProducesProblem(StatusCodes.Status404NotFound)
			             .ProducesProblem(StatusCodes.Status409Conflict);
		}
	}
}
