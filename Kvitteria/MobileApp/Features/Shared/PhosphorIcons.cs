// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Features.Shared;

/// <summary>
/// Code points in the bundled Phosphor face, which XAML reaches through the <c>FontIcons</c> alias
/// in <c>Resources/Styles/Tokens.xaml</c>.
/// </summary>
/// <remarks>
/// Named constants rather than literals in XAML: a private-use code point is unreadable in place
/// and unsearchable, and the product mockup names its icons (<c>ph ph-receipt</c>) rather than
/// numbering them. Values are transcribed from Phosphor's own <c>src/regular/style.css</c>, and the
/// name after the dash there is the name here.
/// Only the icons this app actually draws are listed; the face carries roughly a thousand more.
/// </remarks>
public static class PhosphorIcons
{
	/// <summary>ph-receipt — the brand mark, and the receipts tab.</summary>
	public const string Receipt = "\ue3ec";

	/// <summary>ph-gear — the account control on the overview.</summary>
	public const string Gear = "\ue270";

	/// <summary>ph-sign-out</summary>
	public const string SignOut = "\ue42a";

	/// <summary>ph-warning-circle — the server-unreachable state.</summary>
	public const string WarningCircle = "\ue4e2";

	/// <summary>ph-arrow-clockwise — retry.</summary>
	public const string ArrowClockwise = "\ue036";

	/// <summary>ph-translate — the language control.</summary>
	public const string Translate = "\ue4a2";

	/// <summary>ph-sliders-horizontal — changing server.</summary>
	public const string SlidersHorizontal = "\ue434";

	/// <summary>ph-chart-pie-slice — the overview tab.</summary>
	public const string ChartPieSlice = "\ue15a";

	/// <summary>ph-lightning — the sort tab.</summary>
	public const string Lightning = "\ue2de";

	/// <summary>ph-trend-up — the insights tab.</summary>
	public const string TrendUp = "\ue4ae";

	/// <summary>ph-flow-arrow — the pipeline tab.</summary>
	public const string FlowArrow = "\ue6ec";
}
