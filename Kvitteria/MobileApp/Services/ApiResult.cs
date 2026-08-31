// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Services;

/// <summary>How an <c>/api</c> call ended.</summary>
/// <remarks>
/// One enum for every write path rather than a bespoke one per call, which is where
/// <see cref="MembershipOutcome"/>'s shape stops scaling: five mailbox operations share exactly these
/// failures, and five near-identical enums would only invite them to drift apart.
/// </remarks>
public enum ApiOutcome
{
	Ok,

	/// <summary>400. The server's own validation rejected the body.</summary>
	Invalid,

	/// <summary>404. Someone else removed it, most likely.</summary>
	NotFound,

	/// <summary>409. A duplicate, or a Data Protection key ring that can no longer decrypt.</summary>
	Conflict,

	/// <summary>
	/// 502 — the backend reached out and the *other* server said no: bad credentials, an unknown
	/// folder, a failed TLS handshake. Distinct from every other failure because the detail carries
	/// specifics no client could reconstruct, including the list of folders that do exist.
	/// </summary>
	Verification,

	/// <summary>403. Authenticated, and not permitted.</summary>
	Forbidden,

	/// <summary>401. The token is worthless; the session handler already tried refreshing it.</summary>
	Unauthenticated,

	/// <summary>Nothing answered. Says nothing about the request itself.</summary>
	Unreachable
}

/// <summary>
/// The result of an <c>/api</c> call as a value. Errors are never thrown here, matching
/// <c>KvitteriaApiClient</c>: a view model has to render every one of these, and a thrown exception
/// makes that a matter of remembering to catch.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Value">Set only when <paramref name="Outcome"/> is <see cref="ApiOutcome.Ok"/>.</param>
/// <param name="Detail">
/// The <c>detail</c> of the problem+json body, when there was one. Shown to the user verbatim for
/// <see cref="ApiOutcome.Verification"/> and <see cref="ApiOutcome.Conflict"/>, where the server knows
/// something the app cannot work out for itself.
/// </param>
public sealed record ApiResult<T>(ApiOutcome Outcome, T? Value, string? Detail)
{
	public bool IsOk => Outcome is ApiOutcome.Ok;

	public static ApiResult<T> Ok(T value) => new(ApiOutcome.Ok, value, null);

	public static ApiResult<T> Fail(ApiOutcome outcome, string? detail = null) => new(outcome, default, detail);
}
