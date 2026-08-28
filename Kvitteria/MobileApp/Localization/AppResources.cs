// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp.Localization;

/// <summary>
/// Names the resource set. Its full name is exactly the manifest name MSBuild gives
/// <c>Localization/AppResources.resx</c> (root namespace plus folder), which is what
/// <see cref="System.Resources.ResourceManager"/> is constructed from.
/// </summary>
/// <remarks>
/// Hand-written rather than generated on purpose. <c>ResXFileCodeGenerator</c> is a Visual Studio
/// single-file generator, not an MSBuild target, so a designer file that is not committed simply
/// does not exist on a CI runner — and <c>mobile.yml</c> builds this project on Ubuntu. Nothing here
/// needs generating anyway: strings are read through <see cref="ILocalizationService"/>'s indexer so
/// they can change language at runtime, which generated static properties cannot do.
/// The csproj therefore clears the <c>Generator</c> metadata on both resx files.
/// </remarks>
internal static class AppResources
{
}
