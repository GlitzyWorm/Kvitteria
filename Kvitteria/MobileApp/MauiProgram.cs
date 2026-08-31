// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MobileApp.Features.Auth;
using MobileApp.Features.Overview;
using MobileApp.Features.Overview.SettingViews;
using MobileApp.Features.Shared;
using MobileApp.Localization;
using MobileApp.Services;
using Sharpnado.Tabs;

namespace MobileApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
            .UseSharpnadoTabs(loggerEnable: false)
			.ConfigureFonts(fonts =>
			{
				// The only place a font file is named. XAML refers to these aliases through the
				// FontHeading / FontBody / FontBodyMedium / FontIcons tokens in
				// Resources/Styles/Tokens.xaml, so swapping a face is a change here and nowhere else.
				//
				// Montserrat carries headings and Poppins body, per the design system's
				// --font-heading / --font-body tokens. Only Montserrat Medium is registered:
				// heading weight is capped at 500 by the system, and a face that must not be
				// used is better absent than available.
				fonts.AddFont("Montserrat-Medium.ttf", "KvitteriaHeading");
				fonts.AddFont("Poppins-Regular.ttf", "KvitteriaBody");
				fonts.AddFont("Poppins-Medium.ttf", "KvitteriaBodyMedium");

				// Phosphor, which the product mockup draws every icon from. An icon is a glyph
				// rather than an image here: one file covers every density, and each one takes the
				// TextColor of whatever it sits in. Code points live in
				// Features/Shared/PhosphorIcons.cs. MIT — see licenses/MIT-Phosphor.txt.
				fonts.AddFont("Phosphor-Regular.ttf", "KvitteriaIcons");

				// Still registered: LicensesPage sets OpenSansRegular explicitly for the licence
				// texts, where a neutral face is the right call for a wall of legal prose.
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services
		       .AddAppServices()
		       .AddAppScreens();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		MauiApp app = builder.Build();

		// A markup extension is constructed by the XAML parser rather than by the container, so the
		// one service XAML itself depends on has to be handed over explicitly. This is the only
		// such seam, and it is why LocalizationProvider exists rather than a general service locator.
		LocalizationProvider.Initialize(app.Services.GetRequiredService<ILocalizationService>());

		return app;
	}

	extension(IServiceCollection services)
	{
		/// <summary>
		/// Everything with no view attached: storage, HTTP, session and language.
		/// </summary>
		private IServiceCollection AddAppServices()
		{
			services.AddSingleton<ILocalizationService, LocalizationService>();
			services.AddSingleton<INavigator, ShellNavigator>();

			// Separate from INavigator, which stays about routes and confirmations. Both exist for the
			// same reason: a view model must not reach into Shell.Current.
			services.AddSingleton<IDialogService, ShellDialogService>();

			services.AddSingleton<IServerSettings, ServerSettings>();
			services.AddSingleton<ISessionStore, SecureSessionStore>();
			services.AddSingleton<ISessionService, SessionService>();

			services.AddTransient<AuthenticatedHttpMessageHandler>();

			// Two clients differing by one handler. Neither carries a BaseAddress: the server is not
			// known until someone types it, so every call composes an absolute URI from
			// IServerSettings instead.
			services.AddHttpClient(HttpClients.Anonymous, client => client.Timeout = HttpClients.ProbeTimeout);

			services.AddHttpClient(HttpClients.Api)
			        .AddHttpMessageHandler<AuthenticatedHttpMessageHandler>();

			// GoTrue's client must be the anonymous one: refreshing through the handler that
			// refreshes on 401 would recurse.
			services.AddSingleton<GoTrueClient>();
			services.AddSingleton<KvitteriaApiClient>();
			services.AddSingleton<MailboxApiClient>();

			return services;
		}

		/// <summary>
		/// Pages and their view models. Transient throughout: Shell keeps a page alive for the life
		/// of its route, and a view model shared between two of them would carry one screen's error
		/// message onto another.
		/// </summary>
		private IServiceCollection AddAppScreens()
		{
			services.AddSingleton<AppShell>();

			services.AddTransient<GateViewModel>();
			services.AddTransient<GatePage>();

			services.AddTransient<ServerViewModel>();
			services.AddTransient<ServerPage>();

			services.AddTransient<ClaimViewModel>();
			services.AddTransient<ClaimPage>();

			services.AddTransient<AuthViewModel>();
			services.AddTransient<AuthPage>();

			services.AddTransient<OverviewViewModel>();
			services.AddTransient<OverviewPage>();


			services.AddTransient<AccountTabViewModel>();
			services.AddTransient<InboxTabViewModel>();
			services.AddTransient<SettingsPage>();

			// The sheet is transient like every other screen: resolved fresh each time it is shown, so
			// one mailbox's half-typed password can never appear over the next one.
			services.AddTransient<MailboxSheetViewModel>();
			services.AddTransient<MailboxSheet>();

			return services;
		}
	}
}
