using Backend.Features.Persistence.Model;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

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
