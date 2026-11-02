using System.Reflection;
using Backend.Abstractions;
using Backend.Features.Auth;
using Backend.Features.Endpoints;
using Backend.Features.Extraction;
using Backend.Features.Extraction.LLM;
using Backend.Features.Health;
using Backend.Features.Mail;
using Backend.Features.Persistence;
using Backend.Features.Shared;
using Backend.Features.Persistence.Factory;
using Backend.Features.Update;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
// Configuration is whatever CreateBuilder assembles: appsettings.json, then the environment's
// overlay, then environment variables. Nothing is layered on top, so `Database__ConnectionString`
// and friends are the last word — a file added here would sit above the environment provider and
// silently win over compose.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The OpenAPI generator (docs/openapi) runs this entry point as `GetDocument.Insider` to read the
// document. It has no database and no deployment configuration, yet start-up validation still runs, so it
// is given placeholders. They exist only for that tool: a real host never takes this branch, so a missing
// value there still fails start-up as described in CLAUDE.md.
bool generatingOpenApiDocument = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (generatingOpenApiDocument)
{
	builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
		["Database:ConnectionString"] = "Host=openapi-generation",
		["Ollama:BaseUrl"] = "http://openapi-generation",
		["Ollama:DefaultModel"] = "openapi-generation",
		["Auth:Issuer"] = "http://openapi-generation",
		["Auth:JwksUrl"] = "http://openapi-generation/.well-known/jwks.json",
		["Auth:AllowInsecureJwksTransport"] = "true"
	});
}

// Title and version are fixed so a release-please version bump does not change the committed snapshot
// in docs/openapi; the backend's own version is served by GET /version.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) => {
	document.Info.Title = "Kvitteria";
	document.Info.Version = "v1";
	return Task.CompletedTask;
}));

builder.Services.AddHouseholdAuth(builder.Configuration, builder.Environment);

builder.Services.AddHouseholdHealthChecks();

builder.Services.AddProblemDetails();
builder.Services.AddRequestProtection();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();

builder.Services.AddOptionsSection<DatabaseOptions>(builder.Configuration);
builder.Services.AddOptionsSection<OllamaOptions>(builder.Configuration);
builder.Services.AddDbContext<KvitteriaDbContext>((sp, options) => {
	DatabaseOptions databaseOptions = sp.GetRequiredService<IOptionsMonitor<DatabaseOptions>>().CurrentValue;
	options.UseNpgsql(databaseOptions.ConnectionString);

	// The model name is a seed value, not live configuration: it decides what the first
	// PipelineSettings row asks for, and the row owns it from then on.
	string defaultModel = sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue.DefaultModel;

	options.UseSeeding((context, _) => DatabaseSeeder.Seed(context, defaultModel));
	options.UseAsyncSeeding((context, _, ct) => DatabaseSeeder.SeedAsync(context, defaultModel, ct));
});

// Mailboxes live in the database, so their passwords need protecting at rest. The key ring goes in the
// same database as the ciphertext, so one backup covers both; SetApplicationName is pinned so the
// purpose chain cannot drift with the content root and silently orphan every stored password.
builder.Services.AddDataProtection()
       .SetApplicationName("Kvitteria")
       .PersistKeysToDbContext<KvitteriaDbContext>();
builder.Services.AddSingleton<MailboxSecrets>();
builder.Services.AddSingleton<ReceiptIntakeStore>();
builder.Services.AddSingleton<MailFetcher>();
builder.Services.AddHostedService<MailWorker>();

// OllamaOptions is registered above, alongside the DbContext that seeds the model name from it.
builder.Services.AddHttpClient<OllamaClient>((sp, client) => {
	OllamaOptions ollama = sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;
	client.BaseAddress = new Uri(ollama.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(ollama.RequestTimeoutSeconds);
});
builder.Services.AddSingleton<ExtractionQueue>();
builder.Services.AddHostedService<ExtractionWorker>();

builder.Services.AddScoped<ReceiptIngestionService>();
builder.Services.AddSingleton<PersistenceQueue>();
builder.Services.AddHostedService<ReceiptFactoryWorker>();

builder.Services.AddOptionsSection<UpdateOptions>(builder.Configuration);
builder.Services.AddHttpClient<GitHubReleasesClient>(client => {
	client.BaseAddress = new Uri("https://api.github.com/");
	client.Timeout = TimeSpan.FromSeconds(30);
	// GitHub rejects a request without one.
	client.DefaultRequestHeaders.UserAgent.ParseAdd("Kvitteria");
	client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
});
builder.Services.AddSingleton<UpdateStateStore>();
// A singleton as well as a hosted service, so the endpoints read the instance that does the checking.
builder.Services.AddSingleton<UpdateChecker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<UpdateChecker>());

if (generatingOpenApiDocument)
{
	// The three workers would otherwise start and dial the placeholder database.
	builder.Services.RemoveAll<IHostedService>();
}

WebApplication app = builder.Build();

if (!generatingOpenApiDocument)
{
	await app.MigrateDatabaseAsync();
}

// First, so everything after it sees the client address the entrypoint stated instead of the proxy's.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Ahead of authentication so an unauthenticated flood is turned away before any token is validated.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference();
}

// Both mapped outside the /api group below, so an orchestrator and a bug report can reach them
// without a bearer token.
app.MapHouseholdHealthChecks();
app.MapMetaEndpoints();

// Every route requires an active household member; write routes add their own permission on top,
// and the two policies compose as an AND.
RouteGroupBuilder group = app.MapGroup("/api")
                             .RequireAuthorization(PermissionPolicies.ActiveUser)
                             .RequireRateLimiting(RequestProtection.PolicyName);

group.MapReceiptEndpoints();

app.Run();