using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;
using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	/// <summary>
	/// Interaction logic for AlphanumericControl.xaml
	/// </summary>
	public partial class AlphanumericControl : UserControl, IVirtualControl
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool IsAvailable => true;

		public DisplaySetting DisplaySetting { get; set; }
		public bool IgnoreAspectRatio { get; set; }
		public VirtualDisplay Host { get; set; }

		private const int Dpi = 96;
		private const int SwitchTimeMilliseconds = 150;

		private static readonly AlphaNumericResources Res = AlphaNumericResources.GetInstance();

		private readonly Stopwatch _stopwatch = new Stopwatch();

		private Dictionary<int, double> _switchPercentage;
		private Dictionary<int, SwitchDirection> _switchDirection;

		private ushort[] _data;

		private long _elapsedMilliseconds = 0;
		private bool _aspectRatioSet;

		private WriteableBitmap _writeableBitmap;

		public AlphanumericControl()
		{
			DataContext = this;
			InitializeComponent();
			DisplaySetting = new DisplaySetting();

			SizeChanged += SizeChanged_Event;
			CompositionTarget.Rendering += (o, e) => DrawImage(_writeableBitmap);
		}

		public void Init()
		{
			ObservableExtensions.Subscribe<VirtualDisplayPosition>(Host.WindowResized, pos => CreateImage((int)pos.Width, (int)pos.Height));
		}

		public void CreateImage(int width, int height)
		{
			Logger.Debug("Creating image...");
			Res.Loaded[DisplaySetting.SegmentType][DisplaySetting.StyleDefinition.SegmentWeight].Take(1).Subscribe(_ => {
				DisplaySetting.SetDimensions(width, height);
				if (!_aspectRatioSet) {
					Host.SetDimensions(DisplaySetting.Dim.CanvasWidth, DisplaySetting.Dim.CanvasHeight);
					_aspectRatioSet = true;
				} else {
					Res.Rasterize(DisplaySetting);
				}
				SetBitmap(new WriteableBitmap(width, height, Dpi, Dpi, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent));
			});
		}

		public void UpdateData(ushort[] data)
		{
			if (_switchDirection == null) {
				_switchDirection = new Dictionary<int, SwitchDirection>();
			}

			for (var i = 0; i < DisplaySetting.NumChars * DisplaySetting.NumLines; i++) {
				var onBefore = _data != null && _data[i] != 0;
				var onAfter = data[i] != 0;
				if (onBefore != onAfter) {
					_switchDirection[i] = onAfter ? SwitchDirection.On : SwitchDirection.Off;
				}
			}
			_data = data;

			Logger.Debug("new data: [ {0} ]", string.Join(",", data));
		}

		public void DrawImage(WriteableBitmap writeableBitmap)
		{
			if (_data == null || writeableBitmap == null) {
				//Logger.Debug("Skipping: _data = {0}, writeableBitmap = {1}, count = {2}", _data, writeableBitmap, _segmentsForegroundRasterized.Count);
				return;
			}

			UpdateSwitchStatus(_stopwatch.ElapsedMilliseconds - _elapsedMilliseconds);

			var width = (int)writeableBitmap.Width;
			var height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			var surfaceInfo = new SKImageInfo {
				Width = width,
				Height = height,
				ColorType = SKColorType.Bgra8888,
				AlphaType = SKAlphaType.Premul,
			};
			using (var surface = SKSurface.Create(surfaceInfo, writeableBitmap.BackBuffer, width * 4)) {
				AlphaNumericPainter.DrawDisplay(surface, DisplaySetting, _data);
			}
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		public void RenderSegments(ushort[] data)
		{
			UpdateData(data);
		}

		public void UpdateStyle(RasterizeStyleDefinition styleDef)
		{
			DisplaySetting.ApplyStyle(styleDef);
			Res.Rasterize(DisplaySetting, true);
		}

		public void ClearDisplay()
		{
			_data = new ushort[Res.SegmentSize[DisplaySetting.SegmentType]];
		}

		public void Dispose()
		{
			Res.Clear();
		}

		private void UpdateSwitchStatus(long elapsedMillisecondsSinceLastFrame)
		{
			if (_switchPercentage == null) {
				_switchPercentage = new Dictionary<int, double>();
			}
			var elapsedPercentage = (double)elapsedMillisecondsSinceLastFrame / SwitchTimeMilliseconds;
			for (var i = 0; i < DisplaySetting.NumChars * DisplaySetting.NumLines; i++) {
				if (!_switchDirection.ContainsKey(i)) {
					_switchDirection[i] = SwitchDirection.Idle;
					continue;
				}
				if (!_switchPercentage.ContainsKey(i)) {
					_switchPercentage[i] = 0.0;
					continue;
				}
				switch (_switchDirection[i]) {
					case SwitchDirection.Idle:
						continue;
					case SwitchDirection.On:
						_switchPercentage[i] = Math.Min(1, _switchPercentage[i] + elapsedPercentage);
						if (_switchPercentage[i] >= 1) {
							_switchDirection[i] = SwitchDirection.Idle;
						}
						break;
					case SwitchDirection.Off:
						_switchPercentage[i] = Math.Max(0, _switchPercentage[i] - elapsedPercentage);
						if (_switchPercentage[i] <= 0) {
							_switchDirection[i] = SwitchDirection.Idle;
						}
						break;
				}
			}
		}

		private void SetBitmap(WriteableBitmap bitmap)
		{
			AlphanumericDisplay.Source = _writeableBitmap = bitmap;
		}

		private void SizeChanged_Event(object sender, SizeChangedEventArgs e)
		{
			if (!Host.Resizing) {
				CreateImage((int)e.NewSize.Width, (int)e.NewSize.Height);
			}
		}
	}

	internal enum SwitchDirection
	{
		On, Off, Idle
	}
}
