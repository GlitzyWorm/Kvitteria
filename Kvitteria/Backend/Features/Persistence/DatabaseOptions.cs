using Backend.Abstractions;
namespace Backend.Features.Persistence;

public sealed class DatabaseOptions: IOptions
{
	public static string SectionName => "Database";
	
	public string ConnectionString { get; set; } = "Host=127.0.0.1;Port=54322;Database=postgres;Username=postgres;Password=postgres";
}