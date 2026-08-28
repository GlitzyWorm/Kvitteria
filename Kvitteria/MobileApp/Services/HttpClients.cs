// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Services;

/// <summary>
/// The two named clients this app resolves. They differ by one handler, and confusing them is the
/// kind of mistake that presents as an infinite refresh loop rather than as a compile error, so the
/// names live in one place.
/// </summary>
public static class HttpClients
{
	/// <summary>
	/// No bearer, no refresh. Used for GoTrue itself and for the anonymous probes
	/// (<c>/version</c>, <c>/health/ready</c>).
	/// </summary>
	public const string Anonymous = "anonymous";

	/// <summary>Attaches the bearer and refreshes it. Everything under <c>/api</c>.</summary>
	public const string Api = "api";

	/// <summary>
	/// How long a probe waits before the server counts as unreachable. Short on purpose: this runs
	/// on the splash, and a wrong address should reach the retry screen rather than look like a hang.
	/// </summary>
	public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);
}
