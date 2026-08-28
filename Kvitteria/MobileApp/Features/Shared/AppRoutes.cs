// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Shared;

/// <summary>
/// The Shell routes, and the only place they are spelled. Every one is navigated to absolutely
/// (the leading <c>//</c>): the gate, the server screen and the sign-in screen are states of the
/// application rather than steps in a journey, and a relative push would leave the tab bar sitting
/// on the back stack behind a user who is not signed in.
/// </summary>
public static class AppRoutes
{
	/// <summary>The splash. First in <c>AppShell.xaml</c>, so it is where the app starts.</summary>
	public const string Gate = "//gate";

	public const string Server = "//server";

	public const string Claim = "//claim";

	public const string Auth = "//auth";

	/// <summary>The tab bar. Everything behind the gate.</summary>
	public const string Main = "//main";

	/// <summary>Opens the sign-in screen already switched to sign-up: <c>//auth?mode=up</c>.</summary>
	public const string AuthSignUp = $"{Auth}?mode=up";

	/// <summary>
	/// Opens the sign-in screen in its "no access" state, for a caller GoTrue accepted and the
	/// household did not: <c>//auth?state=rejected</c>.
	/// </summary>
	public const string AuthRejected = $"{Auth}?state=rejected";
}
