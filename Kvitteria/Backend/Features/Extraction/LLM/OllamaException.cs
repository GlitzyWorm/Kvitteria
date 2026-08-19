namespace Backend.Features.Extraction.LLM;

/// <summary>Base for failures talking to Ollama.</summary>
public abstract class OllamaException : Exception
{
	protected OllamaException(string message, Exception? innerException = null)
		: base(message, innerException) { }
}

/// <summary>
/// The request never produced a usable response: connection refused, socket reset, timeout, or a
/// non-success status code. The model never saw the prompt.
/// </summary>
public sealed class OllamaTransportException(string message, Exception? innerException = null)
	: OllamaException(message, innerException);

/// <summary>
/// Ollama responded, but the content is not a usable extraction: empty message, malformed envelope,
/// or content that is not the receipt JSON the schema demands.
/// </summary>
public sealed class OllamaResponseException(string message, Exception? innerException = null)
	: OllamaException(message, innerException);
