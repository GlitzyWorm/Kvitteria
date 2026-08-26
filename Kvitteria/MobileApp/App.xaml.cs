// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;

namespace MobileApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // The Kvitteria palette is dark-only — there is no light ramp designed, so the styles
            // carry flat token colours rather than AppThemeBinding pairs. Without pinning the theme,
            // a phone in light mode would still hand platform chrome (pickers, dialogs, the keyboard,
            // the Android status bar) its light colours over this dark ground.
            UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}