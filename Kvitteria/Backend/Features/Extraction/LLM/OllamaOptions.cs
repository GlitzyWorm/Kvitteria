using Backend.Abstractions;
namespace Backend.Features.Extraction.LLM;

public sealed class OllamaOptions: IOptions
{
	public static string SectionName => "Ollama";
	
	public string BaseUrl { get; set; } = "http://localhost:11434";
	public string Model { get; set; } = "qwen3:4b";
	public bool ThinkingMode { get; set; } = false;
	public int RequestTimeoutSeconds { get; set; } = 1200;
	public int NumCtx { get; set; } = 8192;
	public string KeepAlive { get; set; } = "30m";
}