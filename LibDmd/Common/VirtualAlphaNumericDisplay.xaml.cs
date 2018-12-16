using System;
using System.Windows;
using System.Windows.Media;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual.AlphaNumeric;

namespace LibDmd.Common
{
	public partial class VirtualAlphaNumericDisplay
	{
		public override IVirtualControl VirtualControl => AlphaNumericDisplay;

		private readonly Action<int> _toggleSettings;

		public VirtualAlphaNumericDisplay(DisplaySetting displaySetting, Configuration config, Action<int> toggleSettings)
		{
			LockHeight = true;
			if (config != null) {
				var alphanumConfig = (config.VirtualAlphaNumericDisplay as VirtualAlphaNumericDisplayConfig);
				var pos = alphanumConfig?.GetPosition(displaySetting.Display);
				if (pos != null) {
					Left = pos.Left;
					Top = pos.Top;
					Height = pos.Height;
				} else {
					Height = 120;
				}
				AlwaysOnTop = config.VirtualAlphaNumericDisplay.StayOnTop;
				GripColor = config.VirtualAlphaNumericDisplay.HideGrip ? Brushes.Transparent : Brushes.White;

			} else {
				Height = 120;
			}

			InitializeComponent();
			Initialize();

			_toggleSettings = toggleSettings;

			AlphaNumericDisplay.DisplaySetting = displaySetting;

			SettingsPath.Fill = new SolidColorBrush(Colors.Transparent);
			SettingsButton.MouseEnter += (sender, e) => {
				SettingsPath.Fill = new SolidColorBrush(Color.FromArgb(0x60, 0xff, 0xff, 0xff));
				SettingsPath.Stroke = new SolidColorBrush(Color.FromArgb(0x60, 0x0, 0x0, 0x0));
			};
			SettingsButton.MouseLeave += (sender, e) => {
				SettingsPath.Fill = new SolidColorBrush(Colors.Transparent);
				SettingsPath.Stroke = new SolidColorBrush(Colors.Transparent);
			};
			SettingsButton.MouseLeftButtonDown += ToggleDisplaySettings;
		}

		private void ToggleDisplaySettings(object sender, RoutedEventArgs e)
		{
			_toggleSettings(AlphaNumericDisplay.DisplaySetting.Display);
		}
	}
}
