using System.Text.Json;
using Microsoft.Extensions.Options;
namespace Backend.Features.Extraction.LLM;

public sealed class OllamaClient(
	HttpClient httpClient,
	IOptionsMonitor<OllamaOptions> options,
	ILogger<OllamaClient> logger)
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
	};
	
	public async Task<ExtractedReceipt> ExtractAsync(
		string receiptText,
		string? retryHint,
		double temperature,
		CancellationToken ct)
	{
		OllamaOptions ollamaOptions = options.CurrentValue;
		List<OllamaChatMessage> messages = [
			new OllamaChatMessage("system", ReceiptExtractionPrompt.SystemPrompt),
			new OllamaChatMessage("user", receiptText)
		];

		if (retryHint is not null)
		{
			messages.Add(new OllamaChatMessage("user", retryHint));
		}
		
		var request = new OllamaChatRequest(
			ollamaOptions.Model,
			messages,
			Think: ollamaOptions.ThinkingMode,
			Stream: false,
			Format: ReceiptExtractionPrompt.Schema,
			Options: new OllamaChatOptions(temperature, ollamaOptions.NumCtx),
			KeepAlive: ollamaOptions.KeepAlive);
		
		using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/chat", request, JsonOptions, ct);
		string body = await response.Content.ReadAsStringAsync(ct);
		
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Ollama request failed ({(int)response.StatusCode}): {body}");
		}
		
		var chat = JsonSerializer.Deserialize<OllamaChatResponse>(body, JsonOptions);
		string? content = chat?.Message?.Content;
		if (string.IsNullOrWhiteSpace(content))
		{
			throw new InvalidOperationException("Ollama returned an empty message");
		}
		
		logger.LogDebug(
			"Ollama extraction took {TotalMs} ms ({EvalCount} tokens evaluated) for {PromptLength} chars of receipt text",
			(chat!.TotalDuration ?? 0) / 1_000_000, chat.EvalCount, receiptText.Length);
		
		ExtractedReceipt? receipt;
		try
		{
			receipt = JsonSerializer.Deserialize<ExtractedReceipt>(content, JsonOptions);
		}
		catch (JsonException ex)
		{
			logger.LogWarning(ex, "Ollama returned non-JSON extraction content: {Content}", content);
			throw;
		}
		return receipt ?? throw new InvalidOperationException("Ollama message content was not a receipt JSON document");
	}
	
	private sealed record OllamaChatRequest(
		string Model,
		IReadOnlyList<OllamaChatMessage> Messages,
		bool Think,
		bool Stream,
		JsonElement Format,
		OllamaChatOptions Options,
		string KeepAlive);
	
	private sealed record OllamaChatMessage(string Role, string Content);

	private sealed record OllamaChatOptions(double Temperature, int NumCtx);

	private sealed record OllamaChatResponse(
		OllamaChatMessage? Message,
		bool Done,
		string? DoneReason,
		long? TotalDuration,
		int? EvalCount);
}