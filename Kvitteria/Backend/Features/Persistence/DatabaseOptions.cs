using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Persistence;

public sealed class DatabaseOptions : IOptionsSection
{
	public static string SectionName => "Database";

	// The default points at the local Supabase instance from CLAUDE.md so a fresh clone runs without
	// configuration. Any real deployment must override it via the Database section.
	[Required(AllowEmptyStrings = false)]
	public string ConnectionString { get; set; } = "Host=127.0.0.1;Port=54322;Database=postgres;Username=postgres;Password=postgres";
}
