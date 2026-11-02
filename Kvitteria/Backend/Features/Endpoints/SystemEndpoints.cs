using Backend.Features.Auth;
using Backend.Features.Endpoints.Extensions;
using Backend.Features.Update;
using Kvitteria.Contracts.DTO;
namespace Backend.Features.Endpoints;

static class SystemEndpoints
{
	extension(RouteGroupBuilder app)
	{
		internal void MapSystemEndpoints()
		{
			RouteGroupBuilder systemGroup = app.MapGroup("/system");

			// GET /system/update — readable by anyone, like every other read; the household sees that a
			// release exists whether or not it may install it.
			systemGroup.MapGet("/update", async (UpdateChecker checker, UpdateStateStore store, CancellationToken ct) => {
				           DateTime now = DateTime.UtcNow;
				           string currentVersion = VersionDto.Current.Version;
				           UpdateRun? lastRun = UpdateRun.Parse(await store.ReadStatusAsync(ct), now);

				           return Results.Ok(lastRun.ToDto(
					           currentVersion,
					           checker.Newest,
					           checker.LastCheckedAt,
					           Refusal(checker, store, lastRun, currentVersion, now) == UpdateRefusal.None));
			           })
			           .WithName("GetUpdateStatus")
			           .Produces<UpdateStatusDto>();

			// POST /system/update — no body, for the same reason the agent ignores the request file: nobody
			// gets to choose a version. Answers before anything restarts.
			systemGroup.MapPost("/update", async (UpdateChecker checker, UpdateStateStore store, ILogger<UpdateChecker> logger, CancellationToken ct) => {
				           DateTime now = DateTime.UtcNow;
				           UpdateRun? lastRun = UpdateRun.Parse(await store.ReadStatusAsync(ct), now);
				           string currentVersion = VersionDto.Current.Version;

				           switch (Refusal(checker, store, lastRun, currentVersion, now))
				           {
					           case UpdateRefusal.AlreadyRunning:
						           return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:"An update is already running.");
					           case UpdateRefusal.RestoreRequired:
						           return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:"The last update stopped after changing the database. Run ./scripts/update.sh --restore on the server.");
					           case UpdateRefusal.NothingToInstall:
						           return Results.Problem(statusCode:StatusCodes.Status409Conflict, detail:"No newer release is available.");
					           case UpdateRefusal.NoAgent:
						           return NoAgent();
				           }

				           if (!store.TryRequestUpdate())
				           {
					           logger.LogWarning("The heartbeat is live, but the update request could not be written.");
					           return NoAgent();
				           }

				           return Results.Accepted();
			           })
			           .WithName("StartUpdate")
			           .RequireAuthorization(PermissionPolicies.Name(Permission.ManageSettings))
			           .Produces(StatusCodes.Status202Accepted)
			           .ProducesProblem(StatusCodes.Status409Conflict)
			           .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
		}
	}

	private static UpdateRefusal Refusal(UpdateChecker checker, UpdateStateStore store, UpdateRun? lastRun, string currentVersion, DateTime now) =>
		UpdateGate.Decide(
			checker.Newest is { } newest && ReleaseSelection.IsNewer(newest.Version, currentVersion),
			lastRun,
			UpdateGate.IsAgentAlive(store.ReadHeartbeat(), now));

	private static IResult NoAgent() =>
		Results.Problem(statusCode:StatusCodes.Status503ServiceUnavailable, detail:"This server can't start updates by itself. Run ./scripts/update.sh on the server.");
}
