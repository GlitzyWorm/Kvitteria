// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Auth;

namespace MobileApp.Services;

/// <summary>
/// The one origin every request is built from. One server, not a list: a household runs a stack,
/// and a picker would be a second storage shape and a screen this app has no other use for.
/// </summary>
public interface IServerSettings
{
	/// <summary>The stored origin, or null before one has been accepted. Never has a trailing slash.</summary>
	string? BaseUrl { get; }

	bool HasServer { get; }

	void Set(Uri origin);

	void Clear();

	/// <summary>
	/// Builds an absolute URI for a route. Exists because the base address cannot be set on the
	/// <see cref="HttpClient"/> at registration — it is not known until someone types it — so every
	/// call site composes against the live value instead.
	/// </summary>
	Uri Resolve(string route);
}

/// <inheritdoc />
public sealed class ServerSettings : IServerSettings
{
	private const string PreferenceKey = "server.base_url";

	/// <summary>
	/// Plain <see cref="Preferences"/> rather than <see cref="SecureStorage"/>: an address is not a
	/// secret, and the gate reads it on every launch before anything else can run.
	/// </summary>
	public string? BaseUrl
	{
		get
		{
			string stored = Preferences.Default.Get(PreferenceKey, string.Empty);
			return string.IsNullOrEmpty(stored) ? null : stored;
		}
	}

	public bool HasServer => BaseUrl is not null;

	public void Set(Uri origin) => Preferences.Default.Set(PreferenceKey, ServerUrl.ToStorageString(origin));

	public void Clear() => Preferences.Default.Remove(PreferenceKey);

	public Uri Resolve(string route)
	{
		string? baseUrl = BaseUrl
		                  ?? throw new InvalidOperationException("No server has been configured yet.");

		return new Uri(baseUrl + (route.StartsWith('/') ? route : "/" + route));
	}
}
