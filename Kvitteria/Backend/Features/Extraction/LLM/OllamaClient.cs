using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Extraction.LLM;

public sealed class OllamaClient(
	HttpClient httpClient,
	KvitteriaDbContext dbContext,
	ILogger<OllamaClient> logger)
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
	};

	private const long NanosecondsPerMillisecond = 1_000_000;

	/// <summary>Model output and error bodies are unbounded; cap what reaches logs and exception messages.</summary>
	private const int MaxQuotedLength = 2_000;

	public async Task<ExtractedReceipt> ExtractAsync(
		string receiptText,
		string? retryHint,
		double temperature,
		CancellationToken ct)
	{
		PipelineSettings settings = await dbContext.PipelineSettings.AsNoTracking().FirstAsync(ct);
		List<OllamaChatMessage> messages = [
			new OllamaChatMessage("system", ReceiptExtractionPrompt.SystemPrompt),
			new OllamaChatMessage("user", receiptText)
		];

		if (retryHint is not null)
		{
			messages.Add(new OllamaChatMessage("user", retryHint));
		}

		var request = new OllamaChatRequest(
			settings.OllamaModel,
			messages,
			Think: settings.OllamaThinkingMode,
			Stream: false,
			Format: ReceiptExtractionPrompt.Schema,
			Options: new OllamaChatOptions(temperature, settings.OllamaNumCtx, OllamaThreads.Current),
			KeepAlive: settings.OllamaKeepAlive);

		HttpResponseMessage response;
		try
		{
			response = await httpClient.PostAsJsonAsync("api/chat", request, JsonOptions, ct);
		}
		catch (HttpRequestException ex)
		{
			throw new OllamaTransportException("Ollama request could not be sent.", ex);
		}
		catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
		{
			// HttpClient.Timeout surfaces as TaskCanceledException, not TimeoutException.
			throw new OllamaTransportException($"Ollama request timed out after {httpClient.Timeout}.", ex);
		}

		using (response)
		{
			if (!response.IsSuccessStatusCode)
			{
				string body = await response.Content.ReadAsStringAsync(ct);
				throw new OllamaTransportException($"Ollama request failed ({(int)response.StatusCode}): {Quote(body)}");
			}

			OllamaChatResponse? chat;
			try
			{
				chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct);
			}
			catch (JsonException ex)
			{
				throw new OllamaResponseException("Ollama returned a malformed chat envelope.", ex);
			}

			if (chat is null)
			{
				throw new OllamaResponseException("Ollama returned an empty response body.");
			}

			string? content = chat.Message?.Content;
			if (string.IsNullOrWhiteSpace(content))
			{
				throw new OllamaResponseException("Ollama returned an empty message.");
			}

			if (logger.IsEnabled(LogLevel.Debug))
			{
				logger.LogDebug(
					"Ollama extraction took {TotalMs} ms ({EvalCount} tokens evaluated) for {PromptLength} chars of receipt text",
					(chat.TotalDuration ?? 0)/NanosecondsPerMillisecond, chat.EvalCount, receiptText.Length);
			}

			try
			{
				// The caller owns the retry decision and logs the failure.
				return JsonSerializer.Deserialize<ExtractedReceipt>(content, JsonOptions)
					?? throw new OllamaResponseException("Ollama message content was the JSON literal null.");
			}
			catch (JsonException ex)
			{
				throw new OllamaResponseException($"Ollama returned non-JSON extraction content: {Quote(content)}", ex);
			}
		}
	}

	/// <summary>Names of the models Ollama has already pulled, so a settings update can be checked before saving.</summary>
	public async Task<List<string>> GetAvailableModelsAsync(CancellationToken ct)
	{
		HttpResponseMessage response = await httpClient.GetAsync("api/tags", ct);
		response.EnsureSuccessStatusCode();

		OllamaTagsResponse? tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(JsonOptions, ct);
		return tags?.Models.Select(m => m.Name).ToList() ?? [];
	}

	private static string Quote(string value) =>
		value.Length <= MaxQuotedLength ? value : string.Concat(value.AsSpan(0, MaxQuotedLength), "... [truncated]");

	private sealed record OllamaTagsResponse(List<OllamaTagModel> Models);

	private sealed record OllamaTagModel(string Name);

	private sealed record OllamaChatRequest(
		string Model,
		IReadOnlyList<OllamaChatMessage> Messages,
		bool Think,
		bool Stream,
		JsonElement Format,
		OllamaChatOptions Options,
		string KeepAlive);

	private sealed record OllamaChatMessage(string Role, string Content);

	private sealed record OllamaChatOptions(
		double Temperature,
		int NumCtx,
		[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NumThread);

	private sealed record OllamaChatResponse(
		OllamaChatMessage? Message,
		bool Done,
		string? DoneReason,
		long? TotalDuration,
		int? EvalCount);
}
