using Backend.Features.Auth;
using Backend.Features.Endpoints.DTO;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Users;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Endpoints;

static class SettingsEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapSettingsEndpoints()
		{
			RouteGroupBuilder settingsGroup = app.MapGroup("/settings");

			// GET /settings/permissions — readable by anyone, so a client can explain what it is hiding.
			settingsGroup.MapGet("/permissions", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				             HouseholdSettings? settings = await dbContext.HouseholdSettings
				                                                          .AsNoTracking()
				                                                          .FirstOrDefaultAsync(ct);

				             return settings != null
					             ? Results.Ok(settings.ToDto())
					             : Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:"Household settings are missing; the database was not seeded.");
			             })
			             .WithName("GetMemberPermissions")
			             .Produces<MemberPermissionsDto>()
			             .ProducesProblem(StatusCodes.Status404NotFound);

			// PUT /settings/permissions
			settingsGroup.MapPut("/permissions", async (PutMemberPermissionsDto dto, KvitteriaDbContext dbContext, PermissionCache cache, CancellationToken ct) => {
				             List<string> requested = dto.Permissions ?? [];

				             if (!requested.TryParsePermissions(out List<Permission> permissions, out string? invalidName))
					             return Results.Problem(statusCode:StatusCodes.Status400BadRequest, detail:$"'{invalidName}' is not a known permission.");

				             HouseholdSettings? settings = await dbContext.HouseholdSettings.FirstOrDefaultAsync(ct);

				             if (settings == null)
					             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:"Household settings are missing; the database was not seeded.");

				             settings.MemberPermissions = permissions;
				             await dbContext.SaveChangesAsync(ct);

				             // Every member's effective permissions just changed.
				             cache.Invalidate();
				             return Results.Ok(settings.ToDto());
			             })
			             .WithName("PutMemberPermissions")
			             .RequireAuthorization(PermissionPolicies.Name(Permission.ManageSettings))
			             .Produces<MemberPermissionsDto>()
			             .ProducesProblem(StatusCodes.Status400BadRequest)
			             .ProducesProblem(StatusCodes.Status404NotFound);
		}
	}
}
