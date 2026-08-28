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
/// Which server this app talks to. The first screen a fresh install shows, and the one
/// "skift server" comes back to.
/// </summary>
/// <remarks>
/// The product mockup has no counterpart: it is a web app, so its server is wherever it was served
/// from. This screen is drawn from the same tokens rather than from a reference.
/// </remarks>
public sealed partial class ServerViewModel(
	IServerSettings serverSettings,
	ISessionService sessions,
	KvitteriaApiClient api,
	INavigator navigator,
	ILocalizationService localization) : ViewModelBase(localization)
{
	/// <summary>
	/// The Android emulator's route to the host, on the port <c>.env.example</c> gives Caddy. Debug
	/// only: it is a development convenience, and a shipped app suggesting a loopback address would
	/// be nonsense. Release starts empty behind the placeholder instead.
	/// </summary>
	private const string DebugDefault = "http://10.0.2.2:8080";

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
	public partial string Address { get; set; }

	[ObservableProperty]
	public partial string? Error { get; set; }

	/// <summary>Shown when the address carries a route that is about to be trimmed off it.</summary>
	[ObservableProperty]
	public partial string? Hint { get; set; }

	public bool CanConnect => !string.IsNullOrWhiteSpace(Address) && !IsBusy;

	/// <summary>
	/// Fills the field before the screen is shown. A partial property cannot carry an initializer,
	/// and this has to run per appearance anyway: reached from "skift server" the field should show
	/// the address currently in use, not a blank.
	/// </summary>
	public void Prepare()
	{
		if (!string.IsNullOrEmpty(Address))
		{
			return;
		}

		Address = serverSettings.BaseUrl ?? DefaultAddress;
		Error = null;
	}

	private static string DefaultAddress =>
#if DEBUG
		DebugDefault;
#else
		string.Empty;
#endif

	partial void OnAddressChanged(string value)
	{
		Error = null;
		Hint = ServerUrl.LooksLikeKnownRoute(value) ? Localization["ServerRouteHint"] : null;
	}

	[RelayCommand(CanExecute = nameof(CanConnect))]
	private async Task ConnectAsync()
	{
		if (IsBusy)
		{
			return;
		}

		if (!ServerUrl.TryNormalize(Address, out Uri origin, out ServerUrl.Failure failure))
		{
			Error = Localization[failure switch
			{
				ServerUrl.Failure.Empty => "ServerErrorEmpty",
				ServerUrl.Failure.UnsupportedScheme => "ServerErrorUnsupportedScheme",
				_ => "ServerErrorUnparseable"
			}];

			return;
		}

		IsBusy = true;
		ConnectCommand.NotifyCanExecuteChanged();
		Error = null;

		try
		{
			// Two questions, in this order. /version says whether this is a Kvitteria backend at all
			// — without it, a typo pointing at any host that answers is only discovered at sign-in,
			// as a confusing auth error. /health/ready then says which entry screen comes next.
			if (await api.GetVersionAsync(origin, CancellationToken.None) is null)
			{
				// Separates "nothing is there" from "something is there, but it is not
				// Kvitteria" — two different mistakes needing two different sentences.
				Error = Localization[await api.RespondsAsync(origin, CancellationToken.None)
					? "ServerErrorNotKvitteria"
					: "ServerErrorUnreachable"];

				return;
			}

			HouseholdState household = await api.GetHouseholdStateAsync(origin, CancellationToken.None);

			// Any session in hand belongs to the previous server and is worthless against this one.
			await sessions.ClearAsync();
			serverSettings.Set(origin);
			Address = ServerUrl.ToStorageString(origin);

			await navigator.GoToAsync(household == HouseholdState.Unclaimed ? AppRoutes.Claim : AppRoutes.Auth);
		}
		finally
		{
			IsBusy = false;
			ConnectCommand.NotifyCanExecuteChanged();
		}
	}

}
