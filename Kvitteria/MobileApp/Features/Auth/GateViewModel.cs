// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using MobileApp.Services;

namespace MobileApp.Features.Auth;

/// <summary>
/// Decides where the app belongs on launch. Every other screen in this feature is somewhere this
/// can send you.
/// </summary>
/// <remarks>
/// The order matters and each step exists for a reason:
/// <list type="number">
/// <item>no server configured — nothing else can be asked;</item>
/// <item>the server does not answer — a stored token says nothing about a server that is off;</item>
/// <item>no session — ask the server whether it has been claimed, which decides the next screen;</item>
/// <item>an expiring session — refresh before spending it on a request that would 401;</item>
/// <item>ask the household who this is. On a household with no members at all, this call is what
/// bootstraps the caller into its admin: there is no claim endpoint, and
/// <c>UserProvisioning.Decide</c> settles it as a side effect of the first authenticated request.
/// That is intended, and the claim screen has already said so.</item>
/// </list>
/// </remarks>
public sealed partial class GateViewModel(
	IServerSettings serverSettings,
	ISessionService sessions,
	KvitteriaApiClient api,
	INavigator navigator,
	ILocalizationService localization) : ViewModelBase(localization)
{
	/// <summary>
	/// How long the splash stays bare before admitting it is waiting. A local server answers in
	/// well under this, and a spinner that flashes for 80ms reads as a glitch rather than as progress.
	/// </summary>
	private static readonly TimeSpan SpinnerDelay = TimeSpan.FromMilliseconds(400);

	[ObservableProperty]
	public partial bool ShowSpinner { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsSplash))]
	public partial bool IsUnreachable { get; set; }

	/// <summary>
	/// The branded splash, which is everything that is not the retry state. A property rather than
	/// an inverting converter in XAML: there is exactly one of these, and a converter would be a
	/// resource to register and a second place to look.
	/// </summary>
	public bool IsSplash => !IsUnreachable;

	/// <summary>The address that failed, so the retry screen names it rather than being generic.</summary>
	[ObservableProperty]
	public partial string ServerDisplay { get; set; } = string.Empty;

	[RelayCommand]
	public async Task ResolveAsync()
	{
		if (IsBusy)
		{
			return;
		}

		IsBusy = true;
		IsUnreachable = false;
		ServerDisplay = serverSettings.BaseUrl ?? string.Empty;

		using var spinner = new CancellationTokenSource();
		Task delayed = ShowSpinnerAfterDelayAsync(spinner.Token);

		try
		{
			await ResolveCoreAsync();
		}
		finally
		{
			await spinner.CancelAsync();
			await delayed;
			ShowSpinner = false;
			IsBusy = false;
		}
	}

	[RelayCommand]
	private Task ChangeServerAsync() => navigator.GoToAsync(AppRoutes.Server);

	private async Task ResolveCoreAsync()
	{
		if (serverSettings.BaseUrl is not { } baseUrl)
		{
			await navigator.GoToAsync(AppRoutes.Server);
			return;
		}

		var origin = new Uri(baseUrl);

		// Proves the origin is still a Kvitteria backend before any of the rest is worth trying, and
		// fails fast when it is not — which is what turns "the app hangs" into "the server is off".
		if (await api.GetVersionAsync(origin, CancellationToken.None) is null)
		{
			IsUnreachable = true;
			return;
		}

		StoredSession? session = await sessions.LoadAsync();

		if (session is null)
		{
			await GoToSignInAsync(origin);
			return;
		}

		if (session.IsExpiring(DateTimeOffset.UtcNow)
		    && await sessions.EnsureFreshAsync(CancellationToken.None) is null)
		{
			// EnsureFreshAsync already cleared the session if the refresh was refused; if it failed
			// for a network reason the stored session survives and this is a retry, not a sign-out.
			if (sessions.Session is not null)
			{
				IsUnreachable = true;
				return;
			}

			await GoToSignInAsync(origin);
			return;
		}

		MembershipOutcome membership = await api.GetCurrentUserAsync(CancellationToken.None);

		switch (membership.Result)
		{
			case MembershipResult.Member:
				sessions.SetUser(membership.User);
				await navigator.GoToAsync(AppRoutes.Main);
				break;

			case MembershipResult.NotAMember:
				// A 403, not a 401: GoTrue knows this account and the household does not. The
				// session is kept so the screen can name the address that was refused.
				await navigator.GoToAsync(AppRoutes.AuthRejected);
				break;

			case MembershipResult.Unauthenticated:
				await sessions.ClearAsync();
				await GoToSignInAsync(origin);
				break;

			default:
				IsUnreachable = true;
				break;
		}
	}

	/// <summary>
	/// Sends an unauthenticated caller to whichever of the two entry screens fits this server. An
	/// unknown household state falls through to sign-in: it is the screen that works either way,
	/// and claiming is still reachable from the sign-up half of it.
	/// </summary>
	private async Task GoToSignInAsync(Uri origin)
	{
		HouseholdState household = await api.GetHouseholdStateAsync(origin, CancellationToken.None);

		await navigator.GoToAsync(household == HouseholdState.Unclaimed ? AppRoutes.Claim : AppRoutes.Auth);
	}

	private async Task ShowSpinnerAfterDelayAsync(CancellationToken ct)
	{
		try
		{
			await Task.Delay(SpinnerDelay, ct);
			ShowSpinner = true;
		}
		catch (OperationCanceledException)
		{
			// Resolved before the delay elapsed, which is the case this delay exists for.
		}
	}
}
