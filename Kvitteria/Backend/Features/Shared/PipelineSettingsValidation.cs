using Backend.Features.Endpoints.DTO;
namespace Backend.Features.Shared;

/// <summary>
/// Bounds-checks a pipeline settings update. Mirrors the ranges the fields carried as <c>Options</c>
/// class <c>[Range]</c> attributes before they moved into the database, where data annotations no
/// longer apply.
/// </summary>
public static class PipelineSettingsValidation
{
	public static bool TryValidate(PutPipelineSettingsDto dto, out IResult problem)
	{
		string? error =
			OutOfRange(dto.ImapPollIntervalSeconds, 1, 86_400, nameof(dto.ImapPollIntervalSeconds)) ??
			OutOfRange(dto.ImapMaxMessagesPerPoll, 1, 1_000, nameof(dto.ImapMaxMessagesPerPoll)) ??
			OutOfRange(dto.OllamaNumCtx, 256, 1_048_576, nameof(dto.OllamaNumCtx)) ??
			OutOfRange(dto.OllamaMaxExtractionAttempts, 1, 10, nameof(dto.OllamaMaxExtractionAttempts)) ??
			(string.IsNullOrWhiteSpace(dto.OllamaModel) ? $"{nameof(dto.OllamaModel)} is required." : null) ??
			(string.IsNullOrWhiteSpace(dto.OllamaKeepAlive) ? $"{nameof(dto.OllamaKeepAlive)} is required." : null);

		if (error == null)
		{
			problem = Results.Empty;
			return true;
		}

		problem = Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: error);
		return false;
	}

	private static string? OutOfRange(int value, int min, int max, string name) =>
		value >= min && value <= max ? null : $"{name} must be between {min} and {max}.";
}
