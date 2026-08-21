using Backend.Features.Persistence.Model;
namespace Backend.Features.Endpoints.DTO;

public record PipelineSettingsDto(
	int ImapPollIntervalSeconds,
	int ImapMaxMessagesPerPoll,
	string OllamaModel,
	bool OllamaThinkingMode,
	int OllamaNumCtx,
	string OllamaKeepAlive,
	int OllamaMaxExtractionAttempts,
	double OllamaRetryTemperature,
	decimal OllamaReconciliationToleranceKr);

public record PutPipelineSettingsDto(
	int ImapPollIntervalSeconds,
	int ImapMaxMessagesPerPoll,
	string OllamaModel,
	bool OllamaThinkingMode,
	int OllamaNumCtx,
	string OllamaKeepAlive,
	int OllamaMaxExtractionAttempts,
	double OllamaRetryTemperature,
	decimal OllamaReconciliationToleranceKr);

public static class PipelineSettingsDtoExtension
{
	public static PipelineSettingsDto ToDto(this PipelineSettings settings) =>
		new(
			settings.ImapPollIntervalSeconds,
			settings.ImapMaxMessagesPerPoll,
			settings.OllamaModel,
			settings.OllamaThinkingMode,
			settings.OllamaNumCtx,
			settings.OllamaKeepAlive,
			settings.OllamaMaxExtractionAttempts,
			settings.OllamaRetryTemperature,
			settings.OllamaReconciliationToleranceKr);
}
