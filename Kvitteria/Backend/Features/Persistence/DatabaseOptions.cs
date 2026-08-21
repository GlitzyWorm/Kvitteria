using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Persistence;

public sealed class DatabaseOptions : IOptionsSection
{
	public static string SectionName => "Database";

	// No default on purpose. A default pointing at localhost means a mistyped
	// `Database__ConnectionString` starts the process cleanly and dials 127.0.0.1, which surfaces
	// much later as a connection error naming the wrong host. Empty fails ValidateOnStart instead.
	[Required(AllowEmptyStrings = false)]
	public string ConnectionString { get; set; } = string.Empty;
}
