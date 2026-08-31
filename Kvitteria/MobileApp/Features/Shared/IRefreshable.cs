// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Shared;

/// <summary>
/// A view model with server state behind it, which therefore has to be re-asked rather than resolved
/// once in a constructor.
/// </summary>
/// <remarks>
/// The same reasoning as <c>GateViewModel</c> resolving on every appearance and
/// <c>OverviewViewModel.Refresh()</c> being called from one: Shell keeps a page instance alive for the
/// life of its route, so anything settled in a constructor is settled for the whole session. A tab
/// hosting one of these is refreshed when its page appears and when it becomes the selected tab.
/// </remarks>
public interface IRefreshable
{
	Task RefreshAsync(CancellationToken ct);
}
