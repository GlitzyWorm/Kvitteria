// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Overview.SettingViews;

/// <summary>One household member's IMAP mailbox row: icon, inbox name, address, and active state.</summary>
public partial class MailboxCard : ContentView
{
	public static readonly BindableProperty IconProperty =
		BindableProperty.Create(nameof(Icon), typeof(string), typeof(MailboxCard), string.Empty);

	public static readonly BindableProperty InboxNameProperty =
		BindableProperty.Create(nameof(InboxName), typeof(string), typeof(MailboxCard), string.Empty);

	public static readonly BindableProperty ImapAddressProperty =
		BindableProperty.Create(nameof(ImapAddress), typeof(string), typeof(MailboxCard), string.Empty);

	public static readonly BindableProperty IsActiveProperty =
		BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(MailboxCard), false);

	public MailboxCard()
	{
		InitializeComponent();

		// Bindings inside the card read only its own properties, never an inherited BindingContext.
		BindingContext = this;
	}

	public string Icon
	{
		get => (string)GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public string InboxName
	{
		get => (string)GetValue(InboxNameProperty);
		set => SetValue(InboxNameProperty, value);
	}

	public string ImapAddress
	{
		get => (string)GetValue(ImapAddressProperty);
		set => SetValue(ImapAddressProperty, value);
	}

	public bool IsActive
	{
		get => (bool)GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}
}
