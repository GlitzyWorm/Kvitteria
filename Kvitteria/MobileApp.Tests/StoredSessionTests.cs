// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Auth;

namespace MobileApp.Tests;

/// <summary>
/// When a token counts as spent, and which of GoTrue's two expiry fields is believed.
/// </summary>
public class StoredSessionTests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

	/// <summary>
	/// The absolute instant comes from the server's clock and is correct whenever it is read; the
	/// relative one is only correct at the moment the response arrives. Prefer the former.
	/// </summary>
	[Fact]
	public void Prefers_the_absolute_expiry()
	{
		StoredSession session = StoredSession.FromTokenResponse(
			"access",
			"refresh",
			expiresAt: Now.AddHours(2).ToUnixTimeSeconds(),
			expiresIn: 3600,
			"you@household.dk",
			Now);

		Assert.Equal(Now.AddHours(2), session.ExpiresAt);
	}

	[Fact]
	public void Falls_back_to_the_relative_expiry()
	{
		StoredSession session = StoredSession.FromTokenResponse(
			"access", "refresh", expiresAt: null, expiresIn: 3600, "you@household.dk", Now);

		Assert.Equal(Now.AddSeconds(3600), session.ExpiresAt);
	}

	/// <summary>A response carrying neither is already spent, which sends it straight to a refresh.</summary>
	[Fact]
	public void Neither_expiry_means_expired_now()
	{
		StoredSession session = StoredSession.FromTokenResponse(
			"access", "refresh", expiresAt: null, expiresIn: null, "you@household.dk", Now);

		Assert.Equal(Now, session.ExpiresAt);
		Assert.True(session.IsExpiring(Now));
	}

	[Theory]
	// Comfortably alive.
	[InlineData(600, false)]
	// Inside the skew: a request starting now would very likely arrive after expiry.
	[InlineData(59, true)]
	// Exactly on the boundary, which counts as expiring — the comparison is inclusive.
	[InlineData(60, true)]
	// Already gone.
	[InlineData(-1, true)]
	public void Expires_within_the_refresh_skew(int secondsRemaining, bool expected)
	{
		var session = new StoredSession("access", "refresh", Now.AddSeconds(secondsRemaining), "you@household.dk");

		Assert.Equal(expected, session.IsExpiring(Now));
	}

	[Fact]
	public void The_skew_is_a_minute()
		=> Assert.Equal(TimeSpan.FromSeconds(60), StoredSession.RefreshSkew);

	/// <summary>
	/// The email travels with the session so the "no access" screen can name the address that was
	/// refused. GoTrue's own answer wins over what was typed, since it is the address on the account.
	/// </summary>
	[Fact]
	public void Carries_the_address_it_belongs_to()
	{
		StoredSession session = StoredSession.FromTokenResponse(
			"access", "refresh", null, 3600, "You@Household.dk", Now);

		Assert.Equal("You@Household.dk", session.Email);
	}
}
