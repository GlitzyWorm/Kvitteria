// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using Kvitteria.Contracts.DTO;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using MobileApp.Services;

namespace MobileApp.Features.Overview;

/// <summary>
/// The profile bar the source draws above the overview on a phone, and nothing else yet — the rest
/// of this screen belongs to later work.
/// </summary>
public sealed partial class OverviewViewModel(ISessionService sessions, ILocalizationService localization)
	: ViewModelBase(localization)
{
	[ObservableProperty]
	public partial string Greeting { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string SubLine { get; set; } = string.Empty;

	/// <summary>
	/// Refreshes both lines from the resolved member. Called on every appearance because the
	/// language can change underneath it, and both strings are composed in code rather than bound
	/// through the localize extension.
	/// </summary>
	public void Refresh()
	{
		CurrentUserDto? user = sessions.User;

		Greeting = user is null
			? Localization["AppName"]
			: $"{GreetingForHour(DateTime.Now.Hour)}, {user.DisplayName}";

		// The source reads "Husstand · N medlemmer" here. Listing members is reachable now that
		// GET /api/users is ungated, so this is simply not built yet rather than impossible. Email and
		// role are already on CurrentUserDto, are true for everyone, and answer the question the
		// control beside them raises — which account am I about to sign out of.
		SubLine = user is null
			? string.Empty
			: $"{user.Email} · {Localization[user.Role == nameof(UserRoles.Admin) ? "RoleAdmin" : "RoleMember"]}";
	}

	private string GreetingForHour(int hour) => Localization[hour switch
	{
		< 10 => "GreetingMorning",
		< 18 => "GreetingAfternoon",
		_ => "GreetingEvening"
	}];

	/// <summary>
	/// The role names as the backend spells them. <c>UserRole</c> itself stays server-side —
	/// permissions and roles cross the wire as strings, and Contracts holds no enum for them.
	/// </summary>
	private enum UserRoles
	{
		Admin,
		Member
	}
}
