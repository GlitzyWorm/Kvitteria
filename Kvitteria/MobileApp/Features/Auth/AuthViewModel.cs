// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using MobileApp.Services;

namespace MobileApp.Features.Auth;

/// <summary>
/// The gate: sign in, sign up, and the dead end where GoTrue accepted an account the household did
/// not. One screen with two states, as the mockup has it — the kicker, title and blurb are the same
/// frame either way, and only the body below them changes.
/// </summary>
public sealed partial class AuthViewModel(
	GoTrueClient goTrue,
	KvitteriaApiClient api,
	ISessionService sessions,
	INavigator navigator,
	ILocalizationService localization) : ViewModelBase(localization)
{
	/// <summary>Matches the mockup's own check. Deliberately loose — the server is the real judge.</summary>
	private static readonly Regex EmailShape = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

	/// <summary>GoTrue's own floor, from GOTRUE_PASSWORD_MIN_LENGTH in the shipped compose.</summary>
	private const int MinimumPasswordLength = 8;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Kicker), nameof(Title), nameof(Blurb), nameof(Footnote), nameof(SubmitLabel), nameof(IsSignIn))]
	public partial bool IsSignUp { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Kicker), nameof(Title), nameof(Blurb), nameof(IsGate))]
	public partial bool IsRejected { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
	public partial string Email { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
	public partial string Password { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? Error { get; set; }

	/// <summary>The address that was refused, shown on the no-access screen.</summary>
	[ObservableProperty]
	public partial string RejectedEmail { get; set; } = string.Empty;

	public bool IsGate => !IsRejected;

	/// <summary>
	/// The other half of the segmented control. Two RadioButtons in one group each need their own
	/// two-way bindable flag; deriving one from the other keeps a single piece of state behind them,
	/// so they cannot both end up on.
	/// </summary>
	public bool IsSignIn
	{
		get => !IsSignUp;
		set
		{
			if (value)
			{
				IsSignUp = false;
			}
		}
	}

	public string Kicker => Localization[IsRejected ? "NoAccess" : IsSignUp ? "SignUp" : "SignIn"];

	public string Title => Localization[IsRejected ? "Rejected" : IsSignUp ? "CreateAccount" : "WelcomeBack"];

	public string Blurb => Localization[IsRejected ? "RejectedHelp" : IsSignUp ? "SignUpBlurb" : "SignInBlurb"];

	public string Footnote => Localization[IsSignUp ? "PasswordRule" : "SignInFoot"];

	public string SubmitLabel => Localization[IsSignUp ? "SignUp" : "SignIn"];

	public bool CanSubmit => !IsBusy && Email.Length > 0 && Password.Length > 0;

	/// <summary>
	/// Applied by the page from the route's query string: <c>mode=up</c> from the claim screen,
	/// <c>state=rejected</c> from the gate.
	/// </summary>
	public void Prepare(string? mode, string? state)
	{
		IsRejected = string.Equals(state, "rejected", StringComparison.OrdinalIgnoreCase);
		IsSignUp = !IsRejected && string.Equals(mode, "up", StringComparison.OrdinalIgnoreCase);
		Error = null;
		Password = string.Empty;

		if (IsRejected)
		{
			RejectedEmail = sessions.Session?.Email ?? string.Empty;
		}

		// Re-read every derived string: the language may have changed since this page last appeared.
		RaiseDerived();
	}

	/// <summary>Switching between sign in and sign up clears an error about the other one.</summary>
	partial void OnIsSignUpChanged(bool value) => Error = null;

	[RelayCommand(CanExecute = nameof(CanSubmit))]
	private async Task SubmitAsync()
	{
		string email = Email.Trim().ToLowerInvariant();

		if (!EmailShape.IsMatch(email))
		{
			Error = Localization["AuthErrorEmailRequired"];
			return;
		}

		if (Password.Length < MinimumPasswordLength)
		{
			Error = Localization["AuthErrorPasswordTooShort"];
			return;
		}

		await RunBusyAsync(async () =>
		{
			Error = null;

			AuthOutcome outcome = IsSignUp
				? await goTrue.SignUpAsync(email, Password, CancellationToken.None)
				: await goTrue.SignInAsync(email, Password, CancellationToken.None);

			if (!outcome.Succeeded)
			{
				Error = Localize(outcome.ErrorResourceKey);
				return;
			}

			await sessions.AdoptAsync(outcome.Session!);

			// The password is not needed again, and holding it in a bound property keeps it alive in
			// managed memory for as long as this page does.
			Password = string.Empty;

			await ResolveMembershipAsync();
		});
	}

	/// <summary>
	/// Leaves the no-access screen. The GoTrue account is untouched — this app has no service-role
	/// key and could not delete it if it wanted to.
	/// </summary>
	[RelayCommand]
	private async Task SignOutAsync()
	{
		await sessions.SignOutAsync();

		IsRejected = false;
		IsSignUp = false;
		Email = string.Empty;
		Password = string.Empty;
		Error = null;
		RaiseDerived();
	}

	/// <summary>
	/// Asks the household who this is. On a server with no members this is the request that claims
	/// it — see <see cref="ClaimViewModel"/>.
	/// </summary>
	private async Task ResolveMembershipAsync()
	{
		MembershipOutcome membership = await api.GetCurrentUserAsync(CancellationToken.None);

		switch (membership.Result)
		{
			case MembershipResult.Member:
				sessions.SetUser(membership.User);
				await navigator.GoToAsync(AppRoutes.Main);
				break;

			case MembershipResult.NotAMember:
				RejectedEmail = sessions.Session?.Email ?? Email;
				IsRejected = true;
				RaiseDerived();
				break;

			case MembershipResult.Unauthenticated:
				// GoTrue issued a token the backend will not accept — mismatched issuer or key set.
				// Nothing the user can fix, so say so plainly rather than blaming the password.
				await sessions.ClearAsync();
				Error = Localization["AuthErrorServer"];
				break;

			default:
				Error = Localization["AuthErrorNetwork"];
				break;
		}
	}

	private async Task RunBusyAsync(Func<Task> work)
	{
		if (IsBusy)
		{
			return;
		}

		IsBusy = true;
		SubmitCommand.NotifyCanExecuteChanged();

		try
		{
			await work();
		}
		finally
		{
			IsBusy = false;
			SubmitCommand.NotifyCanExecuteChanged();
		}
	}

	private void RaiseDerived()
	{
		OnPropertyChanged(nameof(Kicker));
		OnPropertyChanged(nameof(Title));
		OnPropertyChanged(nameof(Blurb));
		OnPropertyChanged(nameof(Footnote));
		OnPropertyChanged(nameof(SubmitLabel));
		OnPropertyChanged(nameof(IsGate));
	}
}
