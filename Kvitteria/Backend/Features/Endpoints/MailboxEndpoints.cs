using Backend.Features.Auth;
using Backend.Features.Endpoints.Extensions;
using Backend.Features.Mail;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model;
using Backend.Features.Persistence.Model.Users;
using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class MailboxEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapMailboxEndpoints()
		{
			RouteGroupBuilder mailboxGroup = app.MapGroup("/mailboxes");

			// GET /mailboxes — ungated like every read; the password is not part of the DTO.
			mailboxGroup.MapGet("/", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				            // No Include: ToDto reaches through AppUser, so the projection joins it in.
				            // An Include ahead of a Select is ignored by EF anyway.
				            List<MailboxDto> mailboxes = await dbContext.Mailboxes
				                                                        .AsNoTracking()
				                                                        .OrderBy(m => m.Username)
				                                                        .ThenBy(m => m.Folder)
				                                                        .Select(m => m.ToDto())
				                                                        .ToListAsync(ct);

				            return Results.Ok(mailboxes);
			            })
			            .WithName("GetMailboxes")
			            .Produces<List<MailboxDto>>();

			// GET /mailboxes/{id}
			mailboxGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            Mailbox? mailbox = await dbContext.Mailboxes
				                                              .AsNoTracking()
				                                              .Include(m => m.AppUser)
				                                              .FirstOrDefaultAsync(m => m.Id == id, ct);

				            return mailbox != null
					            ? Results.Ok(mailbox.ToDto())
					            : Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Mailbox with id {id} not found");
			            })
			            .WithName("GetMailboxById")
			            .Produces<MailboxDto>()
			            .ProducesProblem(StatusCodes.Status404NotFound);

			// POST /mailboxes
			mailboxGroup.MapPost("/", async (
				            CreateMailboxDto dto,
				            KvitteriaDbContext dbContext,
				            MailboxSecrets secrets,
				            CancellationToken ct) => {
				            if (!MailboxValidation.TryValidate(dto, out IResult validationProblem))
					            return validationProblem;

				            AppUser? owner = await FindOwnerAsync(dbContext, dto.AppUserId, ct);

				            if (owner == null)
					            return OwnerProblem(dto.AppUserId);

				            var mailbox = new Mailbox {
					            AppUserId = owner.Id,
					            AppUser = owner,
					            Host = dto.Host.Trim(),
					            Port = dto.Port,
					            UseSsl = dto.UseSsl,
					            Username = dto.Username.Trim(),
					            Folder = dto.Folder.Trim(),
					            IsEnabled = dto.IsEnabled,
					            CreatedAt = DateTime.UtcNow
				            };

				            // Prove the settings work before storing them: a mailbox that cannot connect is
				            // otherwise only discoverable from a warning in the poll log.
				            string? failure = await ImapConnection.VerifyAsync(mailbox, dto.Password, ct);

				            if (failure != null)
					            return Results.Problem(statusCode:StatusCodes.Status502BadGateway, detail:failure);

				            mailbox.EncryptedPassword = secrets.Protect(dto.Password);

				            dbContext.Mailboxes.Add(mailbox);

				            if (!await dbContext.TrySaveChangesAsync(ct))
					            return DuplicateProblem(mailbox);

				            return Results.CreatedAtRoute("GetMailboxById", new { id = mailbox.Id }, mailbox.ToDto());
			            })
			            .WithName("CreateMailbox")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.ManageMailboxes))
			            .Produces<MailboxDto>(StatusCodes.Status201Created)
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status409Conflict)
			            .ProducesProblem(StatusCodes.Status502BadGateway);

			// PUT /mailboxes/{id}
			mailboxGroup.MapPut("/{id:guid}", async (
				            Guid id,
				            PutMailboxDto dto,
				            KvitteriaDbContext dbContext,
				            MailboxSecrets secrets,
				            CancellationToken ct) => {
				            if (!MailboxValidation.TryValidate(dto, out IResult validationProblem))
					            return validationProblem;

				            Mailbox? mailbox = await dbContext.Mailboxes
				                                              .Include(m => m.AppUser)
				                                              .FirstOrDefaultAsync(m => m.Id == id, ct);

				            if (mailbox == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Mailbox with id {id} not found");

				            AppUser? owner = await FindOwnerAsync(dbContext, dto.AppUserId, ct);

				            if (owner == null)
					            return OwnerProblem(dto.AppUserId);

				            string password;

				            try
				            {
					            password = dto.Password ?? secrets.Unprotect(mailbox.EncryptedPassword);
				            }
				            catch (MailboxSecretException ex)
				            {
					            return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:ex.Message);
				            }

				            bool connectionChanged =
					            dto.Password != null ||
					            !string.Equals(mailbox.Host, dto.Host.Trim(), StringComparison.Ordinal) ||
					            mailbox.Port != dto.Port ||
					            mailbox.UseSsl != dto.UseSsl ||
					            !string.Equals(mailbox.Username, dto.Username.Trim(), StringComparison.Ordinal) ||
					            !string.Equals(mailbox.Folder, dto.Folder.Trim(), StringComparison.Ordinal);

				            mailbox.AppUserId = owner.Id;
				            mailbox.AppUser = owner;
				            mailbox.Host = dto.Host.Trim();
				            mailbox.Port = dto.Port;
				            mailbox.UseSsl = dto.UseSsl;
				            mailbox.Username = dto.Username.Trim();
				            mailbox.Folder = dto.Folder.Trim();
				            mailbox.IsEnabled = dto.IsEnabled;

				            // Toggling IsEnabled or reassigning the owner touches nothing the server cares
				            // about, so it does not have to cost a round-trip to the mail host.
				            if (connectionChanged)
				            {
					            string? failure = await ImapConnection.VerifyAsync(mailbox, password, ct);

					            if (failure != null)
						            return Results.Problem(statusCode:StatusCodes.Status502BadGateway, detail:failure);
				            }

				            if (dto.Password != null)
					            mailbox.EncryptedPassword = secrets.Protect(dto.Password);

				            // A verified change is worth keeping even if the row now collides, so the reset
				            // failure counters are part of the same save.
				            if (connectionChanged)
				            {
					            mailbox.LastError = null;
					            mailbox.ConsecutiveFailures = 0;
				            }

				            if (!await dbContext.TrySaveChangesAsync(ct))
					            return DuplicateProblem(mailbox);

				            return Results.Ok(mailbox.ToDto());
			            })
			            .WithName("UpdateMailbox")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.ManageMailboxes))
			            .Produces<MailboxDto>()
			            .ProducesProblem(StatusCodes.Status400BadRequest)
			            .ProducesProblem(StatusCodes.Status404NotFound)
			            .ProducesProblem(StatusCodes.Status409Conflict)
			            .ProducesProblem(StatusCodes.Status502BadGateway);

			// DELETE /mailboxes/{id}
			mailboxGroup.MapDelete("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				            Mailbox? mailbox = await dbContext.Mailboxes.FirstOrDefaultAsync(m => m.Id == id, ct);

				            if (mailbox == null)
					            return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"Mailbox with id {id} not found");

				            // Nothing references a mailbox, so this is a real delete. The member it belonged
				            // to is untouched — receipts already collected keep their own history.
				            dbContext.Mailboxes.Remove(mailbox);
				            await dbContext.SaveChangesAsync(ct);

				            return Results.NoContent();
			            })
			            .WithName("DeleteMailbox")
			            .RequireAuthorization(PermissionPolicies.Name(Permission.ManageMailboxes))
			            .Produces(StatusCodes.Status204NoContent)
			            .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}

	/// <summary>
	/// A mailbox must belong to a member who can still own it. Disabled members are excluded: their rows
	/// stay for history, but pointing a live mailbox at one would collect mail for someone who was
	/// deliberately shut out.
	/// </summary>
	/// <remarks>
	/// Tracked deliberately: the result is assigned to <see cref="Mailbox.AppUser"/> so the response DTO
	/// can name the member, and an untracked instance attached to a tracked mailbox would be treated as
	/// a new user to insert.
	/// </remarks>
	private static Task<AppUser?> FindOwnerAsync(KvitteriaDbContext dbContext, Guid appUserId, CancellationToken ct) =>
		dbContext.AppUsers
		         .FirstOrDefaultAsync(u => u.Id == appUserId && u.Status != UserStatus.Disabled, ct);

	private static IResult OwnerProblem(Guid appUserId) =>
		Results.Problem(
			statusCode:StatusCodes.Status400BadRequest,
			detail:$"No active or pending household member with id {appUserId} exists to own this mailbox.");

	private static IResult DuplicateProblem(Mailbox mailbox) =>
		Results.Problem(
			statusCode:StatusCodes.Status409Conflict,
			detail:$"Folder '{mailbox.Folder}' on {mailbox.Host} for '{mailbox.Username}' is already registered for this member.");
}
