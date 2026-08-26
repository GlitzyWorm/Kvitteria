using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;

namespace Backend.Tests;

public class PipelineSettingsValidationTests
{
	private static PutPipelineSettingsDto Valid() => new(
		ImapPollIntervalSeconds: 60,
		ImapMaxMessagesPerPoll: 25,
		OllamaModel: "qwen3:4b",
		OllamaThinkingMode: false,
		OllamaNumCtx: 8192,
		OllamaKeepAlive: "30m",
		OllamaMaxExtractionAttempts: 3,
		OllamaRetryTemperature: 0.3,
		OllamaReconciliationToleranceKr: 0.01M);

	[Fact]
	public void Accepts_valid_settings() =>
		Assert.True(PipelineSettingsValidation.TryValidate(Valid(), out _));

	[Theory]
	[InlineData(0, 25)]
	[InlineData(86_401, 25)]
	public void Rejects_poll_interval_out_of_range(int pollIntervalSeconds, int maxMessagesPerPoll)
	{
		PutPipelineSettingsDto dto = Valid() with { ImapPollIntervalSeconds = pollIntervalSeconds, ImapMaxMessagesPerPoll = maxMessagesPerPoll };
		Assert.False(PipelineSettingsValidation.TryValidate(dto, out _));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1_001)]
	public void Rejects_max_messages_per_poll_out_of_range(int maxMessagesPerPoll)
	{
		PutPipelineSettingsDto dto = Valid() with { ImapMaxMessagesPerPoll = maxMessagesPerPoll };
		Assert.False(PipelineSettingsValidation.TryValidate(dto, out _));
	}

	[Theory]
	[InlineData(255)]
	[InlineData(1_048_577)]
	public void Rejects_num_ctx_out_of_range(int numCtx)
	{
		PutPipelineSettingsDto dto = Valid() with { OllamaNumCtx = numCtx };
		Assert.False(PipelineSettingsValidation.TryValidate(dto, out _));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(11)]
	public void Rejects_max_extraction_attempts_out_of_range(int maxAttempts)
	{
		PutPipelineSettingsDto dto = Valid() with { OllamaMaxExtractionAttempts = maxAttempts };
		Assert.False(PipelineSettingsValidation.TryValidate(dto, out _));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_blank_model_or_keep_alive(string blank)
	{
		Assert.False(PipelineSettingsValidation.TryValidate(Valid() with { OllamaModel = blank }, out _));
		Assert.False(PipelineSettingsValidation.TryValidate(Valid() with { OllamaKeepAlive = blank }, out _));
	}
}
