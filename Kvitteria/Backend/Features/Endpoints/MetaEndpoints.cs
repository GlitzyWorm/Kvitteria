using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints;

static class MetaEndpoints
{
	// Extends WebApplication rather than RouteGroupBuilder like every sibling: those are all mapped
	// inside the /api group, which requires an active household member. Supporting a self-hosted
	// install means being able to establish what version somebody is running before they can sign in.
	extension(WebApplication app)
	{
		internal void MapMetaEndpoints()
		{
			// GET /version — anonymous, and reports only the build. It cannot fail, hence no
			// ProducesProblem.
			app.MapGet("/version", () => Results.Ok(VersionDto.Current))
			   .AllowAnonymous()
			   .WithName("GetVersion")
			   .Produces<VersionDto>();
		}
	}
}
