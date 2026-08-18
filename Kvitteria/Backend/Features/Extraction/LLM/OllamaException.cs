namespace Backend.Features.Extraction.LLM;

/// <summary>Base for failures talking to Ollama, so callers can classify instead of catching <see cref="Exception"/>.</summary>
public abstract class OllamaException : Exception
{
	protected OllamaException(string message, Exception? innerException = null)
		: base(message, innerException) { }
}

/// <summary>
/// The request never produced a usable response: connection refused, socket reset, timeout, or a
/// non-success status code. Retryable, but the model never saw the prompt — so it must NOT be sent a
/// hint claiming its previous answer was malformed.
/// </summary>
public sealed class OllamaTransportException(string message, Exception? innerException = null)
	: OllamaException(message, innerException);

/// <summary>
/// Ollama responded, but the content is not a usable extraction: empty message, malformed envelope,
/// or content that is not the receipt JSON the schema demands. Retryable, and this is the only case
/// where a "your previous response could not be parsed" hint is truthful.
/// </summary>
public sealed class OllamaResponseException(string message, Exception? innerException = null)
	: OllamaException(message, innerException);
