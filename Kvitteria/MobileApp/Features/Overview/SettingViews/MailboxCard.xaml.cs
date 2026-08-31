// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Overview.SettingViews;

/// <summary>
/// My own mailbox as a row: the accent envelope, the folder over the server it lives on, an
/// enabled/disabled tag, and a manage control.
/// </summary>
/// <remarks>
/// It binds to a <see cref="MailboxRowViewModel"/> supplied by whatever hosts it. The first version of
/// this card carried four BindableProperties and set <c>BindingContext = this</c>, which is exactly what
/// a BindableLayout cannot work with: the template sets the context to the item, and a constructor that
/// overwrites it means the item never arrives.
/// </remarks>
public partial class MailboxCard : ContentView
{
	public MailboxCard() => InitializeComponent();
}
