using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using SkiaSharp;

namespace LibDmd.Output.Virtual.AlphaNumeric
{
	public partial class AlphanumericControl
	{
		private const int Dpi = 96;
		private const int SwitchTimeMilliseconds = 150;

		private static readonly SKColor BackgroundColor = SKColors.Black;
		private static readonly AlphaNumericResources Res = AlphaNumericResources.GetInstance();

		private int _call;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private Dictionary<int, double> _switchPercentage;
		private Dictionary<int, SwitchDirection> _switchDirection;

		private ushort[] _data;

		private long _elapsedMilliseconds = 0;
		private bool _aspectRatioSet;

		public void Init()
		{
			ObservableExtensions.Subscribe<DmdPosition>(Host.WindowResized, pos => CreateImage((int)pos.Width, (int)pos.Height));
		}

		public void CreateImage(int width, int height)
		{
			AlphanumericControl.Logger.Debug("Creating image...");
			Res.Loaded[DisplaySetting.SegmentType].Take(1).Subscribe(_ => {
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

			AlphanumericControl.Logger.Debug("new data: [ {0} ]", string.Join(",", data));
		}

		public void DrawImage(WriteableBitmap writeableBitmap)
		{
			if (_data == null || writeableBitmap == null) {
				//Logger.Debug("Skipping: _data = {0}, writeableBitmap = {1}, count = {2}", _data, writeableBitmap, _segmentsForegroundRasterized.Count);
				return;
			}

			UpdateSwitchStatus(_stopwatch.ElapsedMilliseconds - _elapsedMilliseconds);

			if (_call == 0) {
				_stopwatch.Start();
			} else {
				_elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
			}

			var width = (int) writeableBitmap.Width;
			var height = (int) writeableBitmap.Height;

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
	}

	internal enum SwitchDirection
	{
		On, Off, Idle
	}
}