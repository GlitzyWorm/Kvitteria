using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Extraction.LLM;

public sealed class OllamaOptions: IOptionsSection
{
	public static string SectionName => "Ollama";

	// No default, for the same reason as DatabaseOptions.ConnectionString: a localhost fallback turns
	// a mistyped environment variable into a silent misconfiguration rather than a failed start.
	[Required(AllowEmptyStrings = false)]
	public string BaseUrl { get; set; } = string.Empty;

	/// <summary>
	/// Seeds <see cref="Persistence.Model.PipelineSettings.OllamaModel"/> when the database is first
	/// created, and is never read again — the row is authoritative afterwards and is edited through
	/// <c>PUT /api/settings/pipeline</c>, which validates the new name against Ollama's own
	/// <c>/api/tags</c>. Sharing one value with the container that pulls the model is what stops the
	/// two from naming different tags.
	/// </summary>
	[Required(AllowEmptyStrings = false)]
	public string DefaultModel { get; set; } = string.Empty;

	/// <summary>Baked into the typed <see cref="HttpClient"/> at construction, so this cannot live in the database.</summary>
	[Range(1, 7_200)]
	public int RequestTimeoutSeconds { get; set; } = 1200;
}