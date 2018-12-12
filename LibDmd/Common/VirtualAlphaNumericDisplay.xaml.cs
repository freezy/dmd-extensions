using System.Windows;
using System.Windows.Media;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual.AlphaNumeric;
using NLog;

namespace LibDmd.Common
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio (if not disabled)
	/// </summary>
	public partial class VirtualAlphaNumericDisplay : VirtualWindow
	{
		//private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private VirtualAlphaNumericSettings _settingWindow;
		private bool _settingsOpen;
		private readonly Configuration _config;

		public VirtualAlphaNumericDisplay(int displayNumber, int numChars, int numLines, SegmentType segmentType, RasterizeStyleDefinition styleDef, Configuration config)
		{
			InitializeComponent();
			Initialize();

			_config = config;

			LockHeight = true;
			AlphaNumericDisplay.DisplaySetting = new DisplaySetting {
				Display = displayNumber,
				SegmentType = segmentType,
				StyleDefinition = styleDef,
				NumChars = numChars,
				NumLines = numLines
			};

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

		public override IVirtualControl VirtualControl => AlphaNumericDisplay;

		private void ToggleDisplaySettings(object sender, RoutedEventArgs e)
		{
			if (_settingWindow == null) {
				_settingWindow = new VirtualAlphaNumericSettings(AlphaNumericDisplay, Top, Left + Width, _config);
				_settingWindow.IsVisibleChanged += (visibleSender, visibleEvent) => _settingsOpen = (bool)visibleEvent.NewValue;
			}

			if (!_settingsOpen) {
				_settingWindow.Show();
			} else {
				_settingWindow.Hide();
			}
		}
	}
}
