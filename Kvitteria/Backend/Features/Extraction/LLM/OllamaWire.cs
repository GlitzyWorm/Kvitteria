using System.Text.Json;
using System.Text.Json.Serialization;
namespace Backend.Features.Extraction.LLM;

/// <summary>
/// The <c>/api/chat</c> request and its options, shared by <see cref="OllamaClient"/> and the evaluation
/// tool, which sends the same requests with no database behind it, so the two cannot drift.
/// </summary>
internal static class OllamaWire
{
	public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
	};
}

internal sealed record OllamaChatRequest(
	string Model,
	IReadOnlyList<OllamaChatMessage> Messages,
	bool Think,
	bool Stream,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Format,
	OllamaChatOptions Options,
	string KeepAlive);

/// <summary><c>Images</c> serialize as base64, which is what Ollama expects, and are omitted when null.</summary>
internal sealed record OllamaChatMessage(
	string Role,
	string Content,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<byte[]>? Images = null);

internal sealed record OllamaChatOptions(
	double Temperature,
	int NumCtx,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NumThread);

internal sealed record OllamaChatResponse(
	OllamaChatMessage? Message,
	bool Done,
	string? DoneReason,
	long? TotalDuration,
	int? EvalCount);
