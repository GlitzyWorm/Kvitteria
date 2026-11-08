using System.Net;
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

	/// <summary>
	/// Extracts the receipt in <paramref name="receiptText"/> as schema-constrained JSON, following
	/// <paramref name="systemPrompt"/>: <see cref="ReceiptExtractionPrompt.SystemPrompt"/> for mail,
	/// <see cref="ReceiptExtractionPrompt.PhotoSystemPrompt"/> for a transcribed image.
	/// </summary>
	public async Task<ExtractedReceipt> ExtractAsync(
		string receiptText,
		string systemPrompt,
		string? retryHint,
		double temperature,
		CancellationToken ct)
	{
		PipelineSettings settings = await dbContext.PipelineSettings.AsNoTracking().FirstAsync(ct);
		List<OllamaChatMessage> messages = [
			new OllamaChatMessage("system", systemPrompt),
			new OllamaChatMessage("user", receiptText)
		];

		if (retryHint is not null)
		{
			messages.Add(new OllamaChatMessage("user", retryHint));
		}

		OllamaChatResponse chat = await ChatAsync(settings, messages, ReceiptExtractionPrompt.Schema, temperature, ct);
		string content = chat.Message!.Content;

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

	/// <summary>
	/// Writes out the receipt in a JPEG as plain text, once, at temperature 0 and with no output format.
	/// The answer is <see cref="ReceiptTranscription.NoReceiptSentinel"/> when the image shows no receipt;
	/// the caller checks for it.
	/// </summary>
	public async Task<string> TranscribeAsync(byte[] jpeg, CancellationToken ct)
	{
		PipelineSettings settings = await dbContext.PipelineSettings.AsNoTracking().FirstAsync(ct);
		List<OllamaChatMessage> messages = [
			new OllamaChatMessage("system", ReceiptExtractionPrompt.TranscriptionPrompt),
			new OllamaChatMessage("user", "Transcribe this image.", [jpeg])
		];

		OllamaChatResponse chat = await ChatAsync(settings, messages, format: null, temperature: 0, ct);
		string content = chat.Message!.Content.Trim();

		if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.LogDebug(
				"Ollama transcription took {TotalMs} ms ({EvalCount} tokens evaluated) for {ImageBytes} bytes of image, giving {TextLength} chars",
				(chat.TotalDuration ?? 0)/NanosecondsPerMillisecond, chat.EvalCount, jpeg.Length, content.Length);
		}

		return content;
	}

	/// <summary>
	/// What the model can do, from <c>/api/show</c>: <c>vision</c> means it reads images. Throws
	/// <see cref="OllamaTransportException"/> when Ollama does not answer, so a caller can tell "cannot
	/// see" from "cannot ask".
	/// </summary>
	public async Task<IReadOnlyList<string>> GetCapabilitiesAsync(string model, CancellationToken ct)
	{
		HttpResponseMessage response;
		try
		{
			response = await httpClient.PostAsJsonAsync("api/show", new OllamaShowRequest(model), JsonOptions, ct);
		}
		catch (HttpRequestException ex)
		{
			throw new OllamaTransportException("Ollama could not be reached to read the model's capabilities.", ex);
		}
		catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
		{
			throw new OllamaTransportException($"Ollama did not describe the model within {httpClient.Timeout}.", ex);
		}

		using (response)
		{
			// An unknown model is answered with 404: it can see nothing, which is an answer, not an outage.
			if (response.StatusCode == HttpStatusCode.NotFound)
				return [];

			if (!response.IsSuccessStatusCode)
			{
				string body = await response.Content.ReadAsStringAsync(ct);
				throw new OllamaTransportException($"Ollama could not describe the model ({(int)response.StatusCode}): {Quote(body)}");
			}

			try
			{
				OllamaShowResponse? show = await response.Content.ReadFromJsonAsync<OllamaShowResponse>(JsonOptions, ct);
				return show?.Capabilities ?? [];
			}
			catch (JsonException ex)
			{
				throw new OllamaTransportException("Ollama returned a malformed model description.", ex);
			}
		}
	}

	/// <summary>
	/// One non-streaming <c>/api/chat</c> call, with the split every caller relies on: a request that never
	/// reached the model is an <see cref="OllamaTransportException"/>, an answer that is unusable is an
	/// <see cref="OllamaResponseException"/>. A returned response always carries non-empty content.
	/// </summary>
	private async Task<OllamaChatResponse> ChatAsync(
		PipelineSettings settings,
		IReadOnlyList<OllamaChatMessage> messages,
		JsonElement? format,
		double temperature,
		CancellationToken ct)
	{
		var request = new OllamaChatRequest(
			settings.OllamaModel,
			messages,
			Think: settings.OllamaThinkingMode,
			Stream: false,
			Format: format,
			Options: BuildOptions(settings, temperature),
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

			if (string.IsNullOrWhiteSpace(chat.Message?.Content))
			{
				throw new OllamaResponseException("Ollama returned an empty message.");
			}

			return chat;
		}
	}

	/// <summary>The options every call shares, so transcription and extraction cannot drift apart.</summary>
	private static OllamaChatOptions BuildOptions(PipelineSettings settings, double temperature) =>
		new(temperature, settings.OllamaNumCtx);

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

	private sealed record OllamaShowRequest(string Model);

	private sealed record OllamaShowResponse(List<string>? Capabilities);

	private sealed record OllamaChatRequest(
		string Model,
		IReadOnlyList<OllamaChatMessage> Messages,
		bool Think,
		bool Stream,
		[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Format,
		OllamaChatOptions Options,
		string KeepAlive);

	/// <summary><c>Images</c> serialize as base64, which is what Ollama expects, and are omitted when null.</summary>
	private sealed record OllamaChatMessage(
		string Role,
		string Content,
		[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<byte[]>? Images = null);

	private sealed record OllamaChatOptions(double Temperature, int NumCtx);

	private sealed record OllamaChatResponse(
		OllamaChatMessage? Message,
		bool Done,
		string? DoneReason,
		long? TotalDuration,
		int? EvalCount);
}
