// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;
using MobileApp.Features.Auth;

namespace MobileApp.Services;

/// <summary>Where the GoTrue session lives between launches.</summary>
public interface ISessionStore
{
	Task<StoredSession?> LoadAsync();

	Task SaveAsync(StoredSession session);

	Task ClearAsync();
}

/// <inheritdoc />
public sealed class SecureSessionStore : ISessionStore
{
	private const string StorageKey = "session";

	/// <summary>
	/// One JSON blob rather than a key per field. SecureStorage writes are per-key and not atomic
	/// across keys, so three of them can tear into a session with a fresh access token and a spent
	/// refresh token — which is unrecoverable, since the refresh token is single-use.
	/// </summary>
	public async Task<StoredSession?> LoadAsync()
	{
		string? raw;

		try
		{
			raw = await SecureStorage.Default.GetAsync(StorageKey);
		}
		catch (Exception)
		{
			// A rotated or lost platform key ring. There is nothing to recover and nothing to tell
			// the user beyond the sign-in screen they are about to see.
			return null;
		}

		if (string.IsNullOrEmpty(raw))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize(raw, KvitteriaJsonContext.Default.StoredSession);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public Task SaveAsync(StoredSession session)
		=> SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(session, KvitteriaJsonContext.Default.StoredSession));

	public Task ClearAsync()
	{
		SecureStorage.Default.Remove(StorageKey);
		return Task.CompletedTask;
	}
}
