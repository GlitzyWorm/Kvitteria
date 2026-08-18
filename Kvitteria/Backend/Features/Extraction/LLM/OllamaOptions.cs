using System.ComponentModel.DataAnnotations;
using Backend.Abstractions;
namespace Backend.Features.Extraction.LLM;

public sealed class OllamaOptions: IOptionsSection
{
	public static string SectionName => "Ollama";
	
	[Required(AllowEmptyStrings = false)]
	public string BaseUrl { get; set; } = "http://localhost:11434";
	[Required(AllowEmptyStrings = false)]
	public string Model { get; set; } = "qwen3:4b";
	public bool ThinkingMode { get; set; } = false;
	[Range(1, 7_200)]
	public int RequestTimeoutSeconds { get; set; } = 1200;
	[Range(256, 1_048_576)]
	public int NumCtx { get; set; } = 8192;
	public string KeepAlive { get; set; } = "30m";

	[Range(1, 10)]
	public int MaxExtractionAttempts { get; set; } = 3;

	/// <summary>Attempt 1 runs at temperature 0; retries use this.</summary>
	public double RetryTemperature { get; set; } = 0.3;

	/// <summary>
	/// Danish receipts round to øre, so an extraction is treated as reconciled when
	/// <c>grand_total - sum(line_totals) - pant_total</c> is within this many kroner of zero.
	/// Exact equality burns every retry on a 1-øre rounding difference.
	/// </summary>
	public decimal ReconciliationToleranceKr { get; set; } = 0.01M;
}