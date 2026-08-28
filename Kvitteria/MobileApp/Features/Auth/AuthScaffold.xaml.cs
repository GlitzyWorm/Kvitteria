// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Auth;

/// <summary>
/// The kicker / title / blurb frame shared by every screen in this feature. Its content is the part
/// that differs.
/// </summary>
public partial class AuthScaffold : ContentView
{
	public static readonly BindableProperty KickerProperty =
		BindableProperty.Create(nameof(Kicker), typeof(string), typeof(AuthScaffold), string.Empty);

	public static readonly BindableProperty TitleProperty =
		BindableProperty.Create(nameof(Title), typeof(string), typeof(AuthScaffold), string.Empty);

	public static readonly BindableProperty BlurbProperty =
		BindableProperty.Create(nameof(Blurb), typeof(string), typeof(AuthScaffold), string.Empty);

	public AuthScaffold()
	{
		InitializeComponent();
	}

	/// <summary>The tracked-out accent label above the title.</summary>
	public string Kicker
	{
		get => (string)GetValue(KickerProperty);
		set => SetValue(KickerProperty, value);
	}

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	/// <summary>One muted sentence under the title. Always present; these screens never open cold.</summary>
	public string Blurb
	{
		get => (string)GetValue(BlurbProperty);
		set => SetValue(BlurbProperty, value);
	}
}
