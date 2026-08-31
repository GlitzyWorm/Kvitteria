// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Overview.SettingViews;

/// <summary>
/// Another member's mailbox as a row: their initials, their name over the folder, and the same tag and
/// manage control the accent-envelope card carries.
/// </summary>
/// <remarks>
/// A separate view rather than a mode on <see cref="MailboxCard"/>, because the two genuinely differ in
/// what they lead with — the source shows a rounded-square envelope for mine and a circular avatar for
/// theirs, and it reorders the two lines of text with it. They share the row view model.
/// </remarks>
public partial class MemberMailboxCard : ContentView
{
	public MemberMailboxCard() => InitializeComponent();
}
