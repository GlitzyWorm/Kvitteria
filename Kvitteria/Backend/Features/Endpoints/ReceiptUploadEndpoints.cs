using Backend.Features.Auth;
using Backend.Features.Extraction;
using Backend.Features.Extraction.LLM;
using Backend.Features.Mail;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Factory;
using Backend.Features.Uploads;
using Kvitteria.Contracts.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Endpoints;

/// <summary>
/// The image source of receipts: uploading one, fetching the image a receipt was read from, and deleting
/// a receipt, which is what acting on a duplicate flag or a wrong photo takes.
/// </summary>
static class ReceiptUploadEndpoints
{
	/// <summary>An image never changes under its receipt, so a client may keep it for as long as it likes.</summary>
	private const string ImmutableCacheControl = "private, max-age=31536000, immutable";

	private const string VisionCapability = "vision";

	extension(RouteGroupBuilder app)
	{
		public void MapReceiptUploadEndpoints()
		{
			RouteGroupBuilder receiptGroup = app.MapGroup("/receipts");

			// POST /receipts/uploads (multipart/form-data: image, capturedAt?)
			receiptGroup.MapPost("/uploads", async (
				            IFormFile? image,
				            [FromForm] DateTimeOffset? capturedAt,
				            KvitteriaDbContext dbContext,
				            OllamaClient ollama,
				            ReceiptIntakeStore store,
				            ExtractionQueue queue,
				            CurrentUserService currentUserService,
				            HttpContext http,
				            ILoggerFactory loggerFactory,
				            CancellationToken ct) => {
				            if (image is null)
					            return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"The form has no 'image' file.");

				            // Checked before reading, so an oversize part is never copied into memory.
				            if (image.Length > ReceiptImageLimits.MaxBytes)
					            return Results.Problem(
						            statusCode:StatusCodes.Status400BadRequest,
						            detail:$"The image is {image.Length} bytes; at most {ReceiptImageLimits.MaxBytes} bytes are accepted.");

				            byte[] content;
				            await using (Stream stream = image.OpenReadStream())
				            {
					            using var buffer = new MemoryStream((int)image.Length);
					            await stream.CopyToAsync(buffer, ct);
					            content = buffer.ToArray();
				            }

				            if (!ReceiptImageLimits.TryValidate(content, out int width, out int height, out IResult imageProblem))
					            return imageProblem;

				            string model = await dbContext.PipelineSettings
				                                          .AsNoTracking()
				                                          .Select(s => s.OllamaModel)
				                                          .FirstAsync(ct);

				            // Asked per request: one cheap local call, and no cache to go stale when the model changes.
				            try
				            {
					            IReadOnlyList<string> capabilities = await ollama.GetCapabilitiesAsync(model, ct);

					            if (!capabilities.Contains(VisionCapability))
						            return Results.Problem(
							            statusCode:StatusCodes.Status409Conflict,
							            detail:$"The pipeline model '{model}' cannot read images. An admin can switch to a vision model in the pipeline settings.");
				            }
				            catch (OllamaTransportException ex)
				            {
					            // Refusing would lose the upload, and the pipeline already waits out an unavailable Ollama.
					            loggerFactory.CreateLogger(nameof(ReceiptUploadEndpoints))
					                         .LogWarning(ex, "Accepting an upload without checking that '{Model}' can read images.", model);
				            }

				            // The active-user policy already resolved this, so it costs nothing here.
				            CurrentUser? user = await currentUserService.ResolveAsync(http.User, ct);
				            if (user == null)
					            return Results.Problem(statusCode:StatusCodes.Status403Forbidden, detail:"Not a member of this household.");

				            ReceiptRecord? record = await store.RecordUploadAsync(
					            content, width, height, capturedAt?.UtcDateTime, user.Id, ct);

				            if (record == null)
					            return Results.Problem(
						            statusCode:StatusCodes.Status409Conflict,
						            detail:"This exact image has already been uploaded. It is either being read, listed under problems, or kept with its receipt.");

				            // Non-blocking, like the retry endpoint. A refused upload leaves nothing behind.
				            if (!queue.TryEnqueue(record))
				            {
					            await store.DeleteQueuedUploadAsync(record.IntakeId, CancellationToken.None);

					            return Results.Problem(
						            statusCode:StatusCodes.Status503ServiceUnavailable,
						            detail:"The extraction queue is full. Try again in a moment.");
				            }

				            return Results.Accepted(value:new ReceiptUploadDto(record.IntakeId));
			            })
			            .WithName("UploadReceiptImage")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.AddReceipts))
			            // Bearer tokens, not cookies, so there is nothing for a cross-site form to ride on.
			            .DisableAntiforgery()
			            // Cut off by Kestrel before the body is buffered.
			            .WithMetadata(new RequestSizeLimitAttribute(ReceiptImageLimits.MaxRequestBytes))
			            .Accepts<IFormFile>("multipart/form-data")
			            .Produces<ReceiptUploadDto>(StatusCodes.Status202Accepted)
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status403Forbidden)
			            .ProducesProblem(StatusCodes.Status409Conflict)
			            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
			            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

			// GET /receipts/{id}/image
			receiptGroup.MapGet("/{id:guid}/image", async (Guid id, KvitteriaDbContext dbContext, HttpContext http, CancellationToken ct) => {
				            var receipt = await dbContext.Receipts
				                                         .AsNoTracking()
				                                         .Where(r => r.Id == id)
				                                         .Select(r => new { r.ImageId })
				                                         .FirstOrDefaultAsync(ct);

				            if (receipt == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            byte[]? content = receipt.ImageId == null
					            ? null
					            : await dbContext.ReceiptImages
					                             .AsNoTracking()
					                             .Where(img => img.Id == receipt.ImageId)
					                             .Select(img => img.Content)
					                             .FirstOrDefaultAsync(ct);

				            if (content == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} has no image");

				            http.Response.Headers.CacheControl = ImmutableCacheControl;

				            return Results.File(content, "image/jpeg");
			            })
			            .WithName("GetReceiptImage")
			            .Produces(StatusCodes.Status200OK, contentType:"image/jpeg")
			            .ProducesProblem(StatusCodes.Status404NotFound);

			// DELETE /receipts/{id}
			receiptGroup.MapDelete("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(ct);

				            var receipt = await dbContext.Receipts
				                                         .Where(r => r.Id == id)
				                                         .Select(r => new { r.ImageId })
				                                         .FirstOrDefaultAsync(ct);

				            if (receipt == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Receipt with id {id} not found");

				            // Lines and meal coverage cascade, and flags pointing here are nulled by their foreign key.
				            // Merchants, canonical items and aliases are referenced, not owned, and stay.
				            await dbContext.Receipts.Where(r => r.Id == id).ExecuteDeleteAsync(ct);

				            // After the receipt, whose foreign key restricts it.
				            if (receipt.ImageId is { } imageId)
					            await dbContext.ReceiptImages.Where(img => img.Id == imageId).ExecuteDeleteAsync(ct);

				            await transaction.CommitAsync(ct);

				            return Results.NoContent();
			            })
			            .WithName("DeleteReceipt")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.EditReceipts))
			            .Produces(StatusCodes.Status204NoContent)
			            .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}
}
