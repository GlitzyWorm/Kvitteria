// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Auth;

/// <summary>
/// Normalizes what someone types into the server field into the one origin every other request is
/// built from. Deliberately free of MAUI types so <c>MobileApp.Tests</c> can link it as source.
/// </summary>
public static class ServerUrl
{
	/// <summary>
	/// Suffixes a user plausibly pastes because that is the URL they had in front of them — the
	/// Scalar reference lives under one, and a token script prints the other. Both are routes on
	/// the origin rather than part of it, so they are stripped rather than rejected.
	/// </summary>
	private static readonly string[] KnownRoutePrefixes = ["/auth/v1", "/api", "/health", "/scalar", "/openapi"];

	/// <summary>Why a string could not become an origin. The caller maps these onto resource keys.</summary>
	public enum Failure
	{
		None,
		Empty,
		Unparseable,
		UnsupportedScheme
	}

	/// <summary>
	/// Trims, defaults a missing scheme to <c>https</c>, drops any path/query/fragment, and returns
	/// the bare origin. A missing scheme defaults to the secure one on purpose: the insecure case is
	/// the one worth typing out.
	/// </summary>
	public static bool TryNormalize(string? raw, out Uri origin, out Failure failure)
	{
		origin = null!;
		string candidate = (raw ?? string.Empty).Trim();

		if (candidate.Length == 0)
		{
			failure = Failure.Empty;
			return false;
		}

		// Uri rejects "localhost:8080" as relative and misreads it as scheme "localhost" when it
		// does parse, so the scheme is decided here rather than left to Uri.
		if (!candidate.Contains("://", StringComparison.Ordinal))
		{
			candidate = "https://" + candidate;
		}

		if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsed))
		{
			failure = Failure.Unparseable;
			return false;
		}

		if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
		{
			failure = Failure.UnsupportedScheme;
			return false;
		}

		if (string.IsNullOrEmpty(parsed.Host))
		{
			failure = Failure.Unparseable;
			return false;
		}

		// Everything after the authority is discarded rather than preserved: this is an origin, and
		// a pasted /auth/v1 would otherwise end up doubled in every request built from it.
		var builder = new UriBuilder(parsed.Scheme, parsed.Host)
		{
			Path = string.Empty,
			Query = string.Empty,
			Fragment = string.Empty
		};

		// UriBuilder writes the scheme's default port back into the string unless it is told the
		// port is absent, which -1 does.
		builder.Port = parsed.IsDefaultPort ? -1 : parsed.Port;

		origin = builder.Uri;
		failure = Failure.None;
		return true;
	}

	/// <summary>
	/// The origin as it is stored and shown: no trailing slash, so concatenating a route reads the
	/// way it does in the endpoint files.
	/// </summary>
	public static string ToStorageString(Uri origin) => origin.GetLeftPart(UriPartial.Authority);

	/// <summary>
	/// True when <paramref name="raw"/> ends in a route this app knows is not part of the origin.
	/// <see cref="TryNormalize"/> strips those unconditionally; this only exists so the UI can say
	/// so rather than silently rewriting what someone typed.
	/// </summary>
	public static bool LooksLikeKnownRoute(string? raw)
	{
		string candidate = (raw ?? string.Empty).Trim().TrimEnd('/');

		foreach (string prefix in KnownRoutePrefixes)
		{
			if (candidate.EndsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
