// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;
using System.Text.Json.Serialization;
using Kvitteria.Contracts.DTO;
using MobileApp.Features.Auth;

namespace MobileApp.Services;

/// <summary>Sent to <c>/auth/v1/token?grant_type=password</c> and <c>/auth/v1/signup</c>.</summary>
public sealed record PasswordGrantRequest(
	[property: JsonPropertyName("email")] string Email,
	[property: JsonPropertyName("password")] string Password);

/// <summary>Sent to <c>/auth/v1/token?grant_type=refresh_token</c>.</summary>
public sealed record RefreshGrantRequest(
	[property: JsonPropertyName("refresh_token")] string RefreshToken);

/// <summary>
/// Source-generated serialization for every type that crosses the wire or goes into storage.
/// Not an optimization: a Release Android build is trimmed, and reflection-based
/// <see cref="JsonSerializer"/> on a trimmed assembly fails at runtime with an empty result rather
/// than at build time — a failure that a Debug run can never reproduce.
/// </summary>
/// <remarks>
/// Web defaults, matching what the backend's minimal APIs emit: camelCase property names, and
/// case-insensitive on the way back in.
/// </remarks>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GoTrueSession))]
[JsonSerializable(typeof(GoTrueUser))]
[JsonSerializable(typeof(StoredSession))]
[JsonSerializable(typeof(PasswordGrantRequest))]
[JsonSerializable(typeof(RefreshGrantRequest))]
[JsonSerializable(typeof(CurrentUserDto))]
[JsonSerializable(typeof(VersionDto))]
public sealed partial class KvitteriaJsonContext : JsonSerializerContext;
