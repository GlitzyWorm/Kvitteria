// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace MobileApp
{
	/// <summary>
	/// Shows the licence texts shipped as app-package assets. MIT and the SIL Open Font
	/// License both require their notice to accompany the software, and someone who installs
	/// the app never sees the repository.
	/// </summary>
	public partial class LicensesPage : ContentPage
	{
		public LicensesPage()
		{
			InitializeComponent();
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();

			MplText.Text = await ReadAssetAsync("licenses/MPL-2.0.txt");
			MitText.Text = await ReadAssetAsync("licenses/MIT.txt");
			MontserratOflText.Text = await ReadAssetAsync("licenses/OFL-Montserrat.txt");
			PoppinsOflText.Text = await ReadAssetAsync("licenses/OFL-Poppins.txt");
		}

		/// <summary>
		/// Reads a text asset out of the app package. Failing to render a licence must not take the
		/// page down with it, so the fallback names where the text can be read instead.
		/// </summary>
		private static async Task<string> ReadAssetAsync(string logicalName)
		{
			try
			{
				using Stream stream = await FileSystem.OpenAppPackageFileAsync(logicalName);
				using StreamReader reader = new(stream);
				return await reader.ReadToEndAsync();
			}
			catch (Exception ex)
			{
				return $"Could not load {logicalName} ({ex.GetType().Name}). "
					+ "The full texts are at https://mozilla.org/MPL/2.0/, https://opensource.org/license/mit "
					+ "and https://openfontlicense.org.";
			}
		}
	}
}
