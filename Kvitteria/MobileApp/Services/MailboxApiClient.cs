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
/// The household's IMAP mailboxes, and the members who can own one.
/// </summary>
/// <remarks>
/// Separate from <see cref="KvitteriaApiClient"/>, which is cohesively about the gate — version,
/// readiness, membership. Same conventions though: no <c>BaseAddress</c> (the origin is typed by the
/// user, so every call composes an absolute URI through <see cref="IServerSettings.Resolve"/>),
/// source-generated serialization, and failures as values.
///
/// This is where <see cref="MailboxDraft"/> becomes a wire type. The draft deliberately knows nothing
/// about Contracts so it can be linked into MobileApp.Tests; the mapping belongs here, at the boundary
/// where a Contracts reference costs nothing.
/// </remarks>
public sealed class MailboxApiClient(
	IHttpClientFactory httpClientFactory,
	IServerSettings serverSettings,
	ILogger<MailboxApiClient> logger)
{
	private const string MailboxesRoute = "/api/mailboxes";

	public async Task<ApiResult<List<MailboxDto>>> GetMailboxesAsync(CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await Api().GetAsync(serverSettings.Resolve(MailboxesRoute), ct);

			return await ReadAsync(response, KvitteriaJsonContext.Default.ListMailboxDto, ct);
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			logger.LogDebug(exception, "Could not list mailboxes.");
			return ApiResult<List<MailboxDto>>.Fail(ApiOutcome.Unreachable);
		}
	}

	/// <summary>
	/// The members a mailbox may belong to. Readable by any active member — the two user GET routes are
	/// ungated, like every other read in this API.
	/// </summary>
	public async Task<ApiResult<List<AppUserDto>>> GetMembersAsync(CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await Api().GetAsync(serverSettings.Resolve("/api/users"), ct);

			return await ReadAsync(response, KvitteriaJsonContext.Default.ListAppUserDto, ct);
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			logger.LogDebug(exception, "Could not list household members.");
			return ApiResult<List<AppUserDto>>.Fail(ApiOutcome.Unreachable);
		}
	}

	public async Task<ApiResult<MailboxDto>> CreateAsync(MailboxDraft draft, CancellationToken ct)
	{
		var dto = new CreateMailboxDto(
			draft.OwnerId,
			draft.Host.Trim(),
			MailboxDraftValidator.ParsePort(draft),
			draft.UseSsl,
			draft.Username.Trim(),
			draft.Password,
			draft.Folder.Trim(),
			draft.IsEnabled);

		try
		{
			using HttpResponseMessage response = await Api().PostAsJsonAsync(
				serverSettings.Resolve(MailboxesRoute), dto, KvitteriaJsonContext.Default.CreateMailboxDto, ct);

			return await ReadAsync(response, KvitteriaJsonContext.Default.MailboxDto, ct);
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			logger.LogDebug(exception, "Could not create a mailbox.");
			return ApiResult<MailboxDto>.Fail(ApiOutcome.Unreachable);
		}
	}

	/// <summary>
	/// Saves an existing mailbox. An empty password in the draft is sent as null, which is how the
	/// server is told to keep the stored one — the plaintext is never returned, so there was nothing to
	/// prefill and nothing to resend.
	/// </summary>
	public async Task<ApiResult<MailboxDto>> UpdateAsync(Guid id, MailboxDraft draft, CancellationToken ct)
	{
		var dto = new PutMailboxDto(
			draft.OwnerId,
			draft.Host.Trim(),
			MailboxDraftValidator.ParsePort(draft),
			draft.UseSsl,
			draft.Username.Trim(),
			string.IsNullOrWhiteSpace(draft.Password) ? null : draft.Password,
			draft.Folder.Trim(),
			draft.IsEnabled);

		try
		{
			using HttpResponseMessage response = await Api().PutAsJsonAsync(
				serverSettings.Resolve($"{MailboxesRoute}/{id}"), dto, KvitteriaJsonContext.Default.PutMailboxDto, ct);

			return await ReadAsync(response, KvitteriaJsonContext.Default.MailboxDto, ct);
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			logger.LogDebug(exception, "Could not save mailbox {Id}.", id);
			return ApiResult<MailboxDto>.Fail(ApiOutcome.Unreachable);
		}
	}

	/// <summary>Removes a mailbox, and with it a password nobody can recover.</summary>
	public async Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response =
				await Api().DeleteAsync(serverSettings.Resolve($"{MailboxesRoute}/{id}"), ct);

			if (response.IsSuccessStatusCode)
			{
				return ApiResult<bool>.Ok(true);
			}

			string body = await response.Content.ReadAsStringAsync(ct);
			return ApiResult<bool>.Fail(OutcomeFor(response.StatusCode), ProblemDetail.Read(body));
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			logger.LogDebug(exception, "Could not remove mailbox {Id}.", id);
			return ApiResult<bool>.Fail(ApiOutcome.Unreachable);
		}
	}

	private HttpClient Api() => httpClientFactory.CreateClient(HttpClients.Api);

	/// <summary>
	/// Turns one response into a result. The body is read whatever the status, because on a failure it
	/// is the problem+json carrying the only sentence worth showing.
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
