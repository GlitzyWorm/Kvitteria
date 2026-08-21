using Backend.Abstractions;
using Backend.Features.Auth;
using Backend.Features.Endpoints;
using Backend.Features.Extraction;
using Backend.Features.Extraction.LLM;
using Backend.Features.Mail;
using Backend.Features.Persistence;
using Backend.Features.Shared;
using Backend.Features.Persistence.Factory;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// TODO: Testing config.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional:true, reloadOnChange:true);

builder.Services.AddOpenApi();

builder.Services.AddHouseholdAuth(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();

builder.Services.AddOptionsSection<DatabaseOptions>(builder.Configuration);
builder.Services.AddDbContext<KvitteriaDbContext>((sp, options) => {
	DatabaseOptions databaseOptions = sp.GetRequiredService<IOptionsMonitor<DatabaseOptions>>().CurrentValue;
	options.UseNpgsql(databaseOptions.ConnectionString);
	
	options.UseSeeding((context, _) => DatabaseSeeder.Seed(context));
	options.UseAsyncSeeding((context, _, ct) => DatabaseSeeder.SeedAsync(context, ct));
});

// Mailboxes live in the database, so their passwords need protecting at rest. The key ring goes in the
// same database as the ciphertext, so one backup covers both; SetApplicationName is pinned so the
// purpose chain cannot drift with the content root and silently orphan every stored password.
builder.Services.AddDataProtection()
       .SetApplicationName("Kvitteria")
       .PersistKeysToDbContext<KvitteriaDbContext>();
builder.Services.AddSingleton<MailboxSecrets>();
builder.Services.AddSingleton<MailFetcher>();
builder.Services.AddHostedService<MailWorker>();

builder.Services.AddOptionsSection<OllamaOptions>(builder.Configuration);
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

WebApplication app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference();
}

// Every route requires an active household member; write routes add their own permission on top,
// and the two policies compose as an AND.
RouteGroupBuilder group = app.MapGroup("/api")
                             .RequireAuthorization(PermissionPolicies.ActiveUser);

group.MapReceiptEndpoints();

app.Run();