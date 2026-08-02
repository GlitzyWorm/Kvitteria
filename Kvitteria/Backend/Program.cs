using Backend.Features.Mail;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// TODO: Testing config.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<ImapOptions>(builder.Configuration.GetSection(ImapOptions.SectionName));
builder.Services.AddSingleton<MailFetcher>();
builder.Services.AddHostedService<MailWorker>();

WebApplication app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();