using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Extraction.LLM;

public sealed class OllamaOptions: IOptionsSection
{
	public static string SectionName => "Ollama";

	[Required(AllowEmptyStrings = false)]
	public string BaseUrl { get; set; } = "http://localhost:11434";

	/// <summary>Baked into the typed <see cref="HttpClient"/> at construction, so this cannot live in the database.</summary>
	[Range(1, 7_200)]
	public int RequestTimeoutSeconds { get; set; } = 1200;
}