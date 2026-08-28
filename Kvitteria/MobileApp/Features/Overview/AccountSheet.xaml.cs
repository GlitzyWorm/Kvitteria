// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Overview;

/// <summary>The account sheet's content. Shown with the toolkit's popup extension.</summary>
public partial class AccountSheet : ContentView
{
	public AccountSheet(AccountSheetViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	/// <summary>
	/// Exposed so the page that shows this sheet can tell it how to close. Closing is addressed to
	/// the <em>page</em> the popup was shown over, not to the content — the toolkit's extension
	/// takes a Page — and the alternative is this view holding a reference to its host.
	/// </summary>
	public AccountSheetViewModel ViewModel { get; }
}
