using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LibDmd.Output.Virtual;
using NLog;
using SkiaSharp.Extended.Svg;

namespace LibDmd.Common
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio (if not disabled)
	/// </summary>
	public partial class VirtualAlphaNumericDisplay : VirtualWindow
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private VirtualAlphaNumericSettings _settingWindow;
		private bool _settingsOpen = false;

		public VirtualAlphaNumericDisplay(int displayNumber, int numChars, int numLines, SegmentType segmentType)
		{
			InitializeComponent();
			Initialize();

			LockHeight = true;
			AlphaNumericDisplay.DisplaySetting = new DisplaySetting {
				Display = displayNumber,
				SegmentType = segmentType,
				NumChars = numChars,
				NumLines = numLines
			};
		}

		public override IVirtualControl VirtualControl => AlphaNumericDisplay;

		private void ToggleDisplaySettings(object sender, RoutedEventArgs e)
		{
			if (_settingWindow == null) {
				_settingWindow = new VirtualAlphaNumericSettings(AlphaNumericDisplay, Top, Left + Width);
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
