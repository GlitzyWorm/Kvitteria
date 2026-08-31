// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Shared;

namespace MobileApp.Tests;

/// <summary>
/// The client half of a rule the server also enforces — for the four fields
/// PipelineSettingsValidation.TryValidate range-checks (poll interval, messages per poll, context
/// window, extraction attempts) and the two required fields (model, keep-alive). Retry temperature
/// and reconciliation tolerance have no server-side range at all; the bounds checked here are
/// client-only hints adopted from the design reference, and a drift in those two is only ever a UX
/// regression, never a request the server would reject.
/// </summary>
public class PipelineSettingsDraftValidatorTests
{
	private static PipelineSettingsDraft Valid() => new()
	{
		ImapPollIntervalSeconds = "60",
		ImapMaxMessagesPerPoll = "25",
		OllamaModel = "qwen3.5:4b",
		OllamaThinkingMode = false,
		OllamaNumCtx = "8192",
		OllamaKeepAlive = "30m",
		OllamaMaxExtractionAttempts = "3",
		OllamaRetryTemperature = "0.3",
		OllamaReconciliationToleranceKr = "0.01"
	};

	[Fact]
	public void Accepts_a_complete_draft()
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid());

		Assert.False(errors.HasAny);
	}

	[Theory]
	[InlineData("1")]
	[InlineData("86400")]
	public void Accepts_poll_interval_at_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { ImapPollIntervalSeconds = value });

		Assert.Null(errors.ImapPollIntervalSeconds);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("86401")]
	[InlineData("")]
	[InlineData("abc")]
	public void Rejects_poll_interval_outside_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { ImapPollIntervalSeconds = value });

		Assert.Equal("PipelineErrorPollInterval", errors.ImapPollIntervalSeconds);
	}

	[Theory]
	[InlineData("1")]
	[InlineData("1000")]
	public void Accepts_messages_per_poll_at_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { ImapMaxMessagesPerPoll = value });

		Assert.Null(errors.ImapMaxMessagesPerPoll);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("1001")]
	public void Rejects_messages_per_poll_outside_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { ImapMaxMessagesPerPoll = value });

		Assert.Equal("PipelineErrorMessagesPerPoll", errors.ImapMaxMessagesPerPoll);
	}

	[Theory]
	[InlineData("256")]
	[InlineData("1048576")]
	public void Accepts_context_window_at_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaNumCtx = value });

		Assert.Null(errors.OllamaNumCtx);
	}

	[Theory]
	[InlineData("255")]
	[InlineData("1048577")]
	public void Rejects_context_window_outside_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaNumCtx = value });

		Assert.Equal("PipelineErrorNumCtx", errors.OllamaNumCtx);
	}

	[Theory]
	[InlineData("1")]
	[InlineData("10")]
	public void Accepts_extraction_attempts_at_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaMaxExtractionAttempts = value });

		Assert.Null(errors.OllamaMaxExtractionAttempts);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("11")]
	public void Rejects_extraction_attempts_outside_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaMaxExtractionAttempts = value });

		Assert.Equal("PipelineErrorMaxExtractionAttempts", errors.OllamaMaxExtractionAttempts);
	}

	/// <summary>Client-only: the backend does not range-check this field at all.</summary>
	[Theory]
	[InlineData("0")]
	[InlineData("2")]
	public void Accepts_retry_temperature_at_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaRetryTemperature = value });

		Assert.Null(errors.OllamaRetryTemperature);
	}

	[Theory]
	[InlineData("-0.1")]
	[InlineData("2.1")]
	public void Rejects_retry_temperature_outside_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaRetryTemperature = value });

		Assert.Equal("PipelineErrorRetryTemperature", errors.OllamaRetryTemperature);
	}

	/// <summary>Client-only: the backend does not range-check this field at all.</summary>
	[Theory]
	[InlineData("0")]
	[InlineData("100")]
	public void Accepts_reconciliation_tolerance_at_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaReconciliationToleranceKr = value });

		Assert.Null(errors.OllamaReconciliationToleranceKr);
	}

	[Theory]
	[InlineData("-0.01")]
	[InlineData("100.01")]
	public void Rejects_reconciliation_tolerance_outside_the_bounds(string value)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaReconciliationToleranceKr = value });

		Assert.Equal("PipelineErrorReconciliationTolerance", errors.OllamaReconciliationToleranceKr);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_a_missing_model(string model)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaModel = model });

		Assert.Equal("PipelineErrorModelRequired", errors.OllamaModel);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_a_missing_keep_alive(string keepAlive)
	{
		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(Valid() with { OllamaKeepAlive = keepAlive });

		Assert.Equal("PipelineErrorKeepAliveRequired", errors.OllamaKeepAlive);
	}

	[Fact]
	public void Reports_every_broken_field_at_once()
	{
		PipelineSettingsDraft draft = Valid() with { ImapPollIntervalSeconds = "0", OllamaModel = "" };

		PipelineSettingsFieldErrors errors = PipelineSettingsDraftValidator.Validate(draft);

		Assert.NotNull(errors.ImapPollIntervalSeconds);
		Assert.NotNull(errors.OllamaModel);
		Assert.Null(errors.ImapMaxMessagesPerPoll);
		Assert.True(errors.HasAny);
	}

	[Fact]
	public void Parses_every_field_invariantly()
	{
		PipelineSettingsDraft draft = Valid();

		Assert.Equal(60, PipelineSettingsDraftValidator.ParsePollIntervalSeconds(draft));
		Assert.Equal(25, PipelineSettingsDraftValidator.ParseMessagesPerPoll(draft));
		Assert.Equal(8192, PipelineSettingsDraftValidator.ParseNumCtx(draft));
		Assert.Equal(3, PipelineSettingsDraftValidator.ParseMaxExtractionAttempts(draft));
		Assert.Equal(0.3, PipelineSettingsDraftValidator.ParseRetryTemperature(draft));
		Assert.Equal(0.01M, PipelineSettingsDraftValidator.ParseReconciliationToleranceKr(draft));
	}
}
