using Backend.Features.Extraction;
using Backend.Features.Extraction.LLM;
using Backend.Features.Mail;
using Microsoft.Extensions.Options;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// TODO: Testing config.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<ImapOptions>(builder.Configuration.GetSection(ImapOptions.SectionName));
builder.Services.AddSingleton<MailFetcher>();
builder.Services.AddHostedService<MailWorker>();

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.AddHttpClient<OllamaClient>((sp, client) =>
{
	OllamaOptions ollama = sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;
	client.BaseAddress = new Uri(ollama.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(ollama.RequestTimeoutSeconds);
});
builder.Services.AddSingleton<ExtractionQueue>();
builder.Services.AddHostedService<ExtractionWorker>();

WebApplication app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();