// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Kvitteria.Contracts.DTO;
using Microsoft.Extensions.Logging;
using MobileApp.Features.Auth;

namespace MobileApp.Services;

/// <summary>
/// The signed-in session as the rest of the app sees it: the tokens, who they belong to on this
/// server, and the single place a refresh is allowed to happen.
/// </summary>
public interface ISessionService
{
	StoredSession? Session { get; }

	/// <summary>
	/// Who this account is <em>to the household</em>, from <c>GET /api/users/me</c>. Null until the
	/// gate has resolved it — a GoTrue session proves identity, not membership.
	/// </summary>
	CurrentUserDto? User { get; }

	/// <summary>Raised when a refresh failed and the session is gone. The shell returns to the gate.</summary>
	event EventHandler? SessionExpired;

	Task<StoredSession?> LoadAsync();

	Task AdoptAsync(StoredSession session);

	void SetUser(CurrentUserDto? user);

	/// <summary>
	/// Returns a token that is good for the next minute, refreshing first if it is not. Returns null
	/// when there is no session left, which is the caller's cue to stop rather than to retry.
	/// </summary>
	Task<StoredSession?> EnsureFreshAsync(CancellationToken ct);

	/// <summary>
	/// Refreshes even if the stored token still looks valid, unless someone else already replaced
	/// <paramref name="spentAccessToken"/> while this caller waited for the lock. That check is what
	/// keeps a burst of concurrent 401s from spending the rotating refresh token more than once.
	/// </summary>
	Task<StoredSession?> RefreshAsync(string spentAccessToken, CancellationToken ct);

	/// <summary>Tells GoTrue, then forgets the session locally whether or not that worked.</summary>
	Task SignOutAsync();

	/// <summary>Forgets the session locally without telling anyone. Used when the token is already dead.</summary>
	Task ClearAsync();
}

/// <inheritdoc />
public sealed class SessionService(
	ISessionStore store,
	GoTrueClient goTrue,
	ILogger<SessionService> logger) : ISessionService
{
	/// <summary>
	/// GoTrue rotates the refresh token on every use and honours the previous one for only ten
	/// seconds. Two refreshes racing therefore do not merely duplicate work — the loser can spend a
	/// token the winner has already replaced and log the household out. One at a time, always.
	/// </summary>
	private readonly SemaphoreSlim _refreshLock = new(1, 1);

	public StoredSession? Session { get; private set; }

	public CurrentUserDto? User { get; private set; }

	public event EventHandler? SessionExpired;

	public async Task<StoredSession?> LoadAsync()
	{
		Session = await store.LoadAsync();
		return Session;
	}

	public async Task AdoptAsync(StoredSession session)
	{
		Session = session;
		await store.SaveAsync(session);
	}

	public void SetUser(CurrentUserDto? user) => User = user;

	public async Task<StoredSession?> EnsureFreshAsync(CancellationToken ct)
	{
		StoredSession? current = Session;

		if (current is null)
		{
			return null;
		}

		return current.IsExpiring(DateTimeOffset.UtcNow)
			? await RefreshAsync(current.AccessToken, ct)
			: current;
	}

	public async Task<StoredSession?> RefreshAsync(string spentAccessToken, CancellationToken ct)
	{
		await _refreshLock.WaitAsync(ct);

		try
		{
			StoredSession? current = Session;

			if (current is null)
			{
				return null;
			}

			// Someone refreshed while this caller queued. Their result is the answer; asking again
			// would spend a token that is now current.
			if (!string.Equals(current.AccessToken, spentAccessToken, StringComparison.Ordinal))
			{
				return current;
			}

			AuthOutcome outcome = await goTrue.RefreshAsync(current, ct);

			if (outcome.Succeeded)
			{
				await AdoptAsync(outcome.Session!);
				return Session;
			}

			// A network failure is not a dead session — the token may well outlive the outage, and
			// signing the household out because a train went into a tunnel would be wrong.
			if (outcome.ErrorResourceKey == AuthErrorMapper.NetworkKey)
			{
				logger.LogDebug("Refresh could not reach the server; keeping the stored session.");
				return null;
			}

			logger.LogDebug("Refresh was rejected ({Key}); the session is over.", outcome.ErrorResourceKey);
			await ClearAsync();
			SessionExpired?.Invoke(this, EventArgs.Empty);
			return null;
		}
		finally
		{
			_refreshLock.Release();
		}
	}

	public async Task SignOutAsync()
	{
		if (Session is { } session)
		{
			await goTrue.SignOutAsync(session, CancellationToken.None);
		}

		await ClearAsync();
	}

	public async Task ClearAsync()
	{
		Session = null;
		User = null;
		await store.ClearAsync();
	}
}
