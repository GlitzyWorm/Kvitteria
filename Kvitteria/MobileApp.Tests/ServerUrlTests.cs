// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Auth;

namespace MobileApp.Tests;

/// <summary>
/// What someone types into the server field, and the origin it has to become. Every case here is
/// something a person plausibly enters: the address bar contents, the Scalar URL, a trailing slash
/// from a copy-paste, a bare host.
/// </summary>
public class ServerUrlTests
{
	[Theory]
	[InlineData("kvitteria.example.dk", "https://kvitteria.example.dk")]
	[InlineData("  kvitteria.example.dk  ", "https://kvitteria.example.dk")]
	[InlineData("https://kvitteria.example.dk/", "https://kvitteria.example.dk")]
	[InlineData("https://kvitteria.example.dk/api", "https://kvitteria.example.dk")]
	[InlineData("https://kvitteria.example.dk/auth/v1", "https://kvitteria.example.dk")]
	[InlineData("https://kvitteria.example.dk/scalar#tag/receipts", "https://kvitteria.example.dk")]
	[InlineData("HTTPS://Kvitteria.Example.DK", "https://kvitteria.example.dk")]
	public void Normalizes_to_a_bare_origin(string input, string expected)
	{
		Assert.True(ServerUrl.TryNormalize(input, out Uri origin, out ServerUrl.Failure failure));
		Assert.Equal(ServerUrl.Failure.None, failure);
		Assert.Equal(expected, ServerUrl.ToStorageString(origin));
	}

	[Theory]
	[InlineData("10.0.2.2:8080", "https://10.0.2.2:8080")]
	[InlineData("http://10.0.2.2:8080", "http://10.0.2.2:8080")]
	[InlineData("http://localhost:8081/api", "http://localhost:8081")]
	public void Keeps_a_non_default_port(string input, string expected)
	{
		Assert.True(ServerUrl.TryNormalize(input, out Uri origin, out _));
		Assert.Equal(expected, ServerUrl.ToStorageString(origin));
	}

	/// <summary>
	/// The default matters: the insecure case is the one worth having to type out, and a silent
	/// downgrade would be invisible on a screen that only shows the host.
	/// </summary>
	[Fact]
	public void Defaults_a_missing_scheme_to_https()
	{
		Assert.True(ServerUrl.TryNormalize("kvitteria.example.dk", out Uri origin, out _));
		Assert.Equal(Uri.UriSchemeHttps, origin.Scheme);
	}

	/// <summary>
	/// The default port is dropped rather than written back, so two spellings of the same server do
	/// not produce two different stored strings.
	/// </summary>
	[Fact]
	public void Drops_a_default_port()
	{
		Assert.True(ServerUrl.TryNormalize("https://kvitteria.example.dk:443", out Uri origin, out _));
		Assert.Equal("https://kvitteria.example.dk", ServerUrl.ToStorageString(origin));
	}

	[Theory]
	[InlineData("", ServerUrl.Failure.Empty)]
	[InlineData("   ", ServerUrl.Failure.Empty)]
	[InlineData(null, ServerUrl.Failure.Empty)]
	[InlineData("ftp://kvitteria.example.dk", ServerUrl.Failure.UnsupportedScheme)]
	[InlineData("file:///etc/passwd", ServerUrl.Failure.UnsupportedScheme)]
	[InlineData("https://", ServerUrl.Failure.Unparseable)]
	public void Rejects_what_cannot_be_an_origin(string? input, ServerUrl.Failure expected)
	{
		Assert.False(ServerUrl.TryNormalize(input, out _, out ServerUrl.Failure failure));
		Assert.Equal(expected, failure);
	}

	[Theory]
	[InlineData("https://kvitteria.example.dk/api", true)]
	[InlineData("https://kvitteria.example.dk/auth/v1/", true)]
	[InlineData("https://kvitteria.example.dk/HEALTH", true)]
	[InlineData("https://kvitteria.example.dk", false)]
	[InlineData("https://api.example.dk", false)]
	public void Recognizes_a_pasted_route(string input, bool expected)
		=> Assert.Equal(expected, ServerUrl.LooksLikeKnownRoute(input));
}
