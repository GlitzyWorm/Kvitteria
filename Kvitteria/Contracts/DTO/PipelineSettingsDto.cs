namespace Kvitteria.Contracts.DTO;

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