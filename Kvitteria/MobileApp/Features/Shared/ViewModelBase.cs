// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using MobileApp.Localization;

namespace MobileApp.Features.Shared;

/// <summary>
/// What every view model on these screens needs: a busy flag, and a way to say something in the
/// user's language from code rather than from a binding.
/// </summary>
public abstract partial class ViewModelBase(ILocalizationService localization) : ObservableObject
{
	protected ILocalizationService Localization { get; } = localization;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsIdle))]
	public partial bool IsBusy { get; set; }

	/// <summary>
	/// The inverse, for binding a control's IsEnabled. A property rather than an inverting converter
	/// in XAML: it costs one line here and saves registering a resource that every page then has to
	/// remember to reach for.
	/// </summary>
	public bool IsIdle => !IsBusy;

	/// <summary>The string for a resource key. Null in, empty out, so an absent error erases itself.</summary>
	protected string? Localize(string? key) => key is null ? null : Localization[key];
}
