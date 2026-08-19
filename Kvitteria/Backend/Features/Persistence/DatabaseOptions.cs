using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Persistence;

public sealed class DatabaseOptions : IOptionsSection
{
	public static string SectionName => "Database";

	// Defaults to the local development database; override via the Database configuration section.
	[Required(AllowEmptyStrings = false)]
	public string ConnectionString { get; set; } = "Host=127.0.0.1;Port=54322;Database=postgres;Username=postgres;Password=postgres";
}
