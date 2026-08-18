using Backend.Features.Endpoints;
using Backend.Features.Extraction;
using Backend.Features.Extraction.LLM;
using Backend.Features.Mail;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Factory;
using Backend.Features.Persistence.Repository.Implementation;
using Backend.Features.Persistence.Repository.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// TODO: Testing config.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional:true, reloadOnChange:true);

builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.AddDbContext<KvitteriaDbContext>((sp, options) => {
	DatabaseOptions databaseOptions = sp.GetRequiredService<IOptionsMonitor<DatabaseOptions>>().CurrentValue;
	options.UseNpgsql(databaseOptions.ConnectionString);

	// Registered here rather than in OnConfiguring, which re-ran on every DbContext construction.
	// Both callbacks are needed: EF picks the async one on async migration paths.
	options.UseSeeding((context, _) => DatabaseSeeder.Seed(context));
	options.UseAsyncSeeding((context, _, ct) => DatabaseSeeder.SeedAsync(context, ct));
});

builder.Services.Configure<ImapOptions>(builder.Configuration.GetSection(ImapOptions.SectionName));
builder.Services.AddSingleton<MailFetcher>();
builder.Services.AddHostedService<MailWorker>();

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.AddHttpClient<OllamaClient>((sp, client) => {
	OllamaOptions ollama = sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;
	client.BaseAddress = new Uri(ollama.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(ollama.RequestTimeoutSeconds);
});
builder.Services.AddSingleton<ExtractionQueue>();
builder.Services.AddHostedService<ExtractionWorker>();

builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddSingleton<PersistenceQueue>();
builder.Services.AddHostedService<ReceiptFactoryWorker>();

WebApplication app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference();
}

RouteGroupBuilder group = app.MapGroup("/api");

group.MapReceiptEndpoints();

app.Run();