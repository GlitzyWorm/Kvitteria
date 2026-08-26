using Backend.Features.Auth;
using Backend.Features.Endpoints.Extensions;
using Backend.Features.Extraction.LLM;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model;
using Backend.Features.Persistence.Model.Users;
using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;
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

			// GET /settings/pipeline — readable by anyone, so a client can display current tuning.
			settingsGroup.MapGet("/pipeline", async (KvitteriaDbContext dbContext, CancellationToken ct) => {
				             PipelineSettings? settings = await dbContext.PipelineSettings
				                                                         .AsNoTracking()
				                                                         .FirstOrDefaultAsync(ct);

				             return settings != null
					             ? Results.Ok(settings.ToDto())
					             : Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:"Pipeline settings are missing; the database was not seeded.");
			             })
			             .WithName("GetPipelineSettings")
			             .Produces<PipelineSettingsDto>()
			             .ProducesProblem(StatusCodes.Status404NotFound);

			// PUT /settings/pipeline
			settingsGroup.MapPut("/pipeline", async (PutPipelineSettingsDto dto, KvitteriaDbContext dbContext, OllamaClient ollamaClient, CancellationToken ct) => {
				             if (!PipelineSettingsValidation.TryValidate(dto, out IResult validationProblem))
					             return validationProblem;

				             PipelineSettings? settings = await dbContext.PipelineSettings.FirstOrDefaultAsync(ct);

				             if (settings == null)
					             return Results.Problem(statusCode:StatusCodes.Status404NotFound, detail:"Pipeline settings are missing; the database was not seeded.");

				             if (!string.Equals(settings.OllamaModel, dto.OllamaModel, StringComparison.Ordinal))
				             {
					             List<string> available;
					             try
					             {
						             available = await ollamaClient.GetAvailableModelsAsync(ct);
					             }
					             catch (HttpRequestException ex)
					             {
						             return Results.Problem(statusCode:StatusCodes.Status502BadGateway, detail:$"Could not reach Ollama to verify '{dto.OllamaModel}' is pulled: {ex.Message}");
					             }

					             if (!available.Contains(dto.OllamaModel))
					             {
						             return Results.Problem(
							             statusCode:StatusCodes.Status400BadRequest,
							             detail:$"'{dto.OllamaModel}' is not pulled in Ollama. Pulled models: {string.Join(", ", available)}. " +
							                    $"Run 'docker compose exec ollama ollama pull {dto.OllamaModel}' first.");
					             }
				             }

				             settings.ImapPollIntervalSeconds = dto.ImapPollIntervalSeconds;
				             settings.ImapMaxMessagesPerPoll = dto.ImapMaxMessagesPerPoll;
				             settings.OllamaModel = dto.OllamaModel;
				             settings.OllamaThinkingMode = dto.OllamaThinkingMode;
				             settings.OllamaNumCtx = dto.OllamaNumCtx;
				             settings.OllamaKeepAlive = dto.OllamaKeepAlive;
				             settings.OllamaMaxExtractionAttempts = dto.OllamaMaxExtractionAttempts;
				             settings.OllamaRetryTemperature = dto.OllamaRetryTemperature;
				             settings.OllamaReconciliationToleranceKr = dto.OllamaReconciliationToleranceKr;

				             await dbContext.SaveChangesAsync(ct);
				             return Results.Ok(settings.ToDto());
			             })
			             .WithName("PutPipelineSettings")
			             .RequireAuthorization(PermissionPolicies.Name(Permission.ManageSettings))
			             .Produces<PipelineSettingsDto>()
			             .ProducesProblem(StatusCodes.Status400BadRequest)
			             .ProducesProblem(StatusCodes.Status404NotFound)
			             .ProducesProblem(StatusCodes.Status502BadGateway);
		}
	}
}
