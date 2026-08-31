// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kvitteria.Contracts.DTO;
using Microsoft.Extensions.Logging;
using MobileApp.Features.Shared;

namespace MobileApp.Services;

/// <summary>
/// The household's pipeline settings: mail-poll tuning and the Ollama extraction model.
/// </summary>
/// <remarks>
/// Self-contained on purpose, like <see cref="MailboxApiClient"/> — no shared HTTP base class, no
/// <c>BaseAddress</c>, source-generated serialization, failures as values.
///
/// This is where <see cref="PipelineSettingsDraft"/> becomes a wire type. The draft deliberately
/// knows nothing about Contracts so it can be linked into MobileApp.Tests; the mapping belongs
/// here, at the boundary where a Contracts reference costs nothing.
/// </remarks>
public sealed class SettingsApiClient(
	IHttpClientFactory httpClientFactory,
	IServerSettings serverSettings,
	ILogger<SettingsApiClient> logger)
{
	private const string PipelineRoute = "/api/settings/pipeline";
	private const string OllamaModelsRoute = "/api/settings/pipeline/ollama-models";

	public async Task<ApiResult<PipelineSettingsDto>> GetPipelineSettingsAsync(CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await Api().GetAsync(serverSettings.Resolve(PipelineRoute), ct);

			return await ReadAsync(response, KvitteriaJsonContext.Default.PipelineSettingsDto, ct);
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			logger.LogDebug(exception, "Could not load pipeline settings.");
			return ApiResult<PipelineSettingsDto>.Fail(ApiOutcome.Unreachable);
		}
	}

	/// <summary>
	/// The models Ollama has actually pulled, for the Model picker. A failure here is reported
	/// separately from the settings themselves — the mail-collection fields are still worth editing
	/// even when Ollama cannot be reached to list its models.
	/// </summary>
	public async Task<ApiResult<OllamaModelsDto>> GetOllamaModelsAsync(CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await Api().GetAsync(serverSettings.Resolve(OllamaModelsRoute), ct);

			return await ReadAsync(response, KvitteriaJsonContext.Default.OllamaModelsDto, ct);
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			logger.LogDebug(exception, "Could not list Ollama models.");
			return ApiResult<OllamaModelsDto>.Fail(ApiOutcome.Unreachable);
		}
	}

	public async Task<ApiResult<PipelineSettingsDto>> UpdatePipelineSettingsAsync(PipelineSettingsDraft draft, CancellationToken ct)
	{
		var dto = new PutPipelineSettingsDto(
			PipelineSettingsDraftValidator.ParsePollIntervalSeconds(draft),
			PipelineSettingsDraftValidator.ParseMessagesPerPoll(draft),
			draft.OllamaModel.Trim(),
			draft.OllamaThinkingMode,
			PipelineSettingsDraftValidator.ParseNumCtx(draft),
			draft.OllamaKeepAlive.Trim(),
			PipelineSettingsDraftValidator.ParseMaxExtractionAttempts(draft),
			PipelineSettingsDraftValidator.ParseRetryTemperature(draft),
			PipelineSettingsDraftValidator.ParseReconciliationToleranceKr(draft));

		try
		{
			using HttpResponseMessage response = await Api().PutAsJsonAsync(
				serverSettings.Resolve(PipelineRoute), dto, KvitteriaJsonContext.Default.PutPipelineSettingsDto, ct);

			return await ReadAsync(response, KvitteriaJsonContext.Default.PipelineSettingsDto, ct);
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			logger.LogDebug(exception, "Could not save pipeline settings.");
			return ApiResult<PipelineSettingsDto>.Fail(ApiOutcome.Unreachable);
		}
	}

	private HttpClient Api() => httpClientFactory.CreateClient(HttpClients.Api);

	/// <summary>
	/// Turns one response into a result. The body is read whatever the status, because on a failure
	/// it is the problem+json carrying the only sentence worth showing.
	/// </summary>
	private async Task<ApiResult<T>> ReadAsync<T>(
		HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
		CancellationToken ct)
	{
		string body = await response.Content.ReadAsStringAsync(ct);

		if (!response.IsSuccessStatusCode)
		{
			ApiOutcome outcome = OutcomeFor(response.StatusCode);
			logger.LogDebug("{Method} answered {Status}.", typeof(T).Name, (int)response.StatusCode);
			return ApiResult<T>.Fail(outcome, ProblemDetail.Read(body));
		}

		T? value = JsonSerializer.Deserialize(body, typeInfo);

		// A 200 whose body will not deserialize is not success in any sense the caller can use.
		return value is null
			? ApiResult<T>.Fail(ApiOutcome.Unreachable)
			: ApiResult<T>.Ok(value);
	}

	private static ApiOutcome OutcomeFor(HttpStatusCode status) => status switch
	{
		HttpStatusCode.BadRequest => ApiOutcome.Invalid,
		HttpStatusCode.Unauthorized => ApiOutcome.Unauthenticated,
		HttpStatusCode.Forbidden => ApiOutcome.Forbidden,
		HttpStatusCode.NotFound => ApiOutcome.NotFound,
		HttpStatusCode.Conflict => ApiOutcome.Conflict,
		HttpStatusCode.BadGateway => ApiOutcome.Verification,
		_ => ApiOutcome.Unreachable
	};

	private static bool IsTransport(Exception exception) =>
		exception is HttpRequestException or TaskCanceledException or JsonException;
}
