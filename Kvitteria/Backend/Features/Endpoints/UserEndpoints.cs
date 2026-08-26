using System.Data;
using Backend.Features.Auth;
using Backend.Features.Endpoints.Extensions;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Users;
using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Backend.Features.Endpoints;

static class UserEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapUserEndpoints()
		{
			RouteGroupBuilder userGroup = app.MapGroup("/users");

			// GET /users/me
			userGroup.MapGet("/me", async (CurrentUserService currentUserService, HttpContext http, CancellationToken ct) => {
				         // The active-user policy already resolved this, so it costs nothing here.
				         CurrentUser? user = await currentUserService.ResolveAsync(http.User, ct);

				         return user != null
					         ? Results.Ok(user.ToDto())
					         : Results.Problem(statusCode:StatusCodes.Status403Forbidden, detail:"Not a member of this household.");
			         })
			         .WithName("GetCurrentUser")
			         .Produces<CurrentUserDto>()
			         .ProducesProblem(StatusCodes.Status403Forbidden);

			// GET /users
			userGroup.MapGet("/", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				         List<AppUserDto> users = await dbContext.AppUsers
				                                                 .AsNoTracking()
				                                                 .OrderBy(u => u.Email)
				                                                 .Select(u => u.ToDto())
				                                                 .ToListAsync(ct);

				         return Results.Ok(users);
			         })
			         .WithName("GetUsers")
			         .RequireAuthorization(PermissionPolicies.Name(Permission.ManageUsers))
			         .Produces<List<AppUserDto>>();

			// GET /users/{id}
			userGroup.MapGet("/{id:guid}", async (Guid id, KvitteriaDbContext dbContext, CancellationToken ct) => {
				         AppUser? user = await dbContext.AppUsers
				                                        .AsNoTracking()
				                                        .FirstOrDefaultAsync(u => u.Id == id, ct);

				         return user != null
					         ? Results.Ok(user.ToDto())
					         : Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"User with id {id} not found");
			         })
			         .WithName("GetUserById")
			         .RequireAuthorization(PermissionPolicies.Name(Permission.ManageUsers))
			         .Produces<AppUserDto>()
			         .ProducesProblem(StatusCodes.Status404NotFound);

			// POST /users
			userGroup.MapPost("/", async (CreateAppUserDto dto, KvitteriaDbContext dbContext, PermissionCache cache, CancellationToken ct) => {
				         if (string.IsNullOrWhiteSpace(dto.Email))
					         return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"Email is required");

				         if (!TryParseRole(dto.Role, UserRole.Member, out UserRole role))
					         return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"'{dto.Role}' is not a known role.");

				         string email = dto.Email.Trim();

				         var user = new AppUser {
					         Email = email,
					         DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? email : dto.DisplayName.Trim(),
					         Role = role,

					         // Pending until its owner signs up against GoTrue and claims it.
					         Status = UserStatus.Pending,
					         CreatedAt = DateTime.UtcNow
				         };

				         dbContext.AppUsers.Add(user);
				         if (!await dbContext.TrySaveChangesAsync(ct))
					         return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:$"A user with email '{email}' already exists.");

				         cache.Invalidate();
				         return Results.CreatedAtRoute("GetUserById", new { id = user.Id }, user.ToDto());
			         })
			         .WithName("CreateUser")
			         .RequireAuthorization(PermissionPolicies.Name(Permission.ManageUsers))
			         .Produces<AppUserDto>(StatusCodes.Status201Created)
			         .ProducesProblem(StatusCodes.Status400BadRequest)
			         .ProducesProblem(StatusCodes.Status409Conflict);

			// PATCH /users/{id}
			userGroup.MapPatch("/{id:guid}", async (Guid id, PatchAppUserDto dto, KvitteriaDbContext dbContext, PermissionCache cache, CancellationToken ct) => {
				         // Serializable, because the last-admin guard acts on a count it then relies on:
				         // two concurrent demotions could otherwise each see the other admin and leave none.
				         await using IDbContextTransaction transaction =
					         await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

				         AppUser? user = await dbContext.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);

				         if (user == null)
					         return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:$"User with id {id} not found");

				         if (!TryParseRole(dto.Role, user.Role, out UserRole role))
					         return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"'{dto.Role}' is not a known role.");

				         if (!TryParseStatus(dto.Status, user.Status, out UserStatus status))
					         return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"'{dto.Status}' is not a known status.");

				         int otherActiveAdmins = await dbContext.AppUsers
				                                                .CountAsync(u => u.Id != id &&
				                                                                 u.Role == UserRole.Admin &&
				                                                                 u.Status == UserStatus.Active, ct);

				         bool isActiveAdmin = user is { Role: UserRole.Admin, Status: UserStatus.Active };

				         if (PermissionResolution.RemovesLastAdmin(isActiveAdmin, otherActiveAdmins, role, status))
					         return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:"The household must keep at least one active admin.");

				         if (!string.IsNullOrWhiteSpace(dto.DisplayName))
					         user.DisplayName = dto.DisplayName.Trim();

				         user.Role = role;
				         user.Status = status;

				         await dbContext.SaveChangesAsync(ct);
				         await transaction.CommitAsync(ct);

				         cache.Invalidate();
				         return Results.Ok(user.ToDto());
			         })
			         .WithName("UpdateUser")
			         .RequireAuthorization(PermissionPolicies.Name(Permission.ManageUsers))
			         .Produces<AppUserDto>()
			         .ProducesProblem(StatusCodes.Status400BadRequest)
			         .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}

	/// <summary>An omitted role leaves the existing one alone; an unknown one is a client error.</summary>
	private static bool TryParseRole(string? name, UserRole fallback, out UserRole role)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			role = fallback;
			return true;
		}

		return name.TryParseName(out role);
	}

	private static bool TryParseStatus(string? name, UserStatus fallback, out UserStatus status)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			status = fallback;
			return true;
		}

		return name.TryParseName(out status);
	}
}
