using System.Diagnostics;
using System.Windows;

namespace LibDmd.Common
{
	public partial class AboutDialog
	{
		public AboutDialog(Point p)
		{
			Left = p.X - 200;
			Top = p.Y - 20;
			InitializeComponent();
			PathUtil.GetAssemblyVersion(out var fullVersion, out var _);
			VersionLabel.Content = $"v{fullVersion}";

			MoreInfoButton.Click += MoreInfoButtonClicked;
			CloseButton.Click += CloseButtonClicked;
		}

		private static void MoreInfoButtonClicked(object sender, RoutedEventArgs e)
		{
			Process.Start(new ProcessStartInfo("https://github.com/freezy/dmd-extensions"));
		}

		private void CloseButtonClicked(object sender, RoutedEventArgs e)
		{
			Close();
		}

	}
}
