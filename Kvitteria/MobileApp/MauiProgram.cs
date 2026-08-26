// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace MobileApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    // The only place a font file is named. XAML refers to these aliases through the
                    // FontHeading / FontBody / FontBodyMedium tokens in Resources/Styles/Tokens.xaml,
                    // so swapping a face is a change here and nowhere else.
                    //
                    // Montserrat carries headings and Poppins body, per the design system's
                    // --font-heading / --font-body tokens. Only Montserrat Medium is registered:
                    // heading weight is capped at 500 by the system, and a face that must not be
                    // used is better absent than available.
                    fonts.AddFont("Montserrat-Medium.ttf", "KvitteriaHeading");
                    fonts.AddFont("Poppins-Regular.ttf", "KvitteriaBody");
                    fonts.AddFont("Poppins-Medium.ttf", "KvitteriaBodyMedium");

                    // Still registered: LicensesPage sets OpenSansRegular explicitly for the licence
                    // texts, where a neutral face is the right call for a wall of legal prose.
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
