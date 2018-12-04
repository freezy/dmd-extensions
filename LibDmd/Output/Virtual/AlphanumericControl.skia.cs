using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Converters;
using System.Windows.Media.Imaging;
using NLog;
using SkiaSharp;
using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

namespace LibDmd.Output.Virtual
{
	public partial class AlphanumericControl
	{
		private const int SwitchTimeMilliseconds = 150;

		private readonly SKColor _backgroundColor = SKColors.Black;
		private readonly AlphaNumericResources _res = AlphaNumericResources.GetInstance();

		private int _call;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private Dictionary<int, double> _switchPercentage;
		private Dictionary<int, SwitchDirection> _switchDirection;

		private ushort[] _data;

		private long _elapsedMilliseconds = 0;
		private bool _aspectRatioSet;

		public void Init()
		{
			Host.WindowResized.Subscribe(pos => CreateImage((int)pos.Width, (int)pos.Height));
		}

		public void CreateImage(int width, int height)
		{
			Logger.Debug("Creating image...");
			if (!_res.SvgsLoaded) {
				Logger.Debug("Segments unavailable, waiting...");
				_res.Loaded[DisplaySetting.SegmentType].Take(1).Subscribe(segments => {
					Logger.Debug("Got segments, setting up shit");
					CreateImageWhenSvgsLoaded(width, height);
				});
			} else {
				Logger.Debug("Segments available, let's go!");
				CreateImageWhenSvgsLoaded(width, height);
			}
		}

		private void CreateImageWhenSvgsLoaded(int width, int height)
		{
			DisplaySetting.OnSvgsLoaded(_res.GetSvgSize(DisplaySetting.SegmentType), width, height);
			if (!_aspectRatioSet) {
				Host.SetDimensions(DisplaySetting.Dim.CanvasWidth, DisplaySetting.Dim.CanvasHeight);
				_aspectRatioSet = true;
			} else {
				_res.Rasterize(DisplaySetting);
			}
			SetBitmap(new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent));
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
				using (var paint = new SKPaint { Color = SKColors.Gray, TextSize = 10 }) {
					var canvas = surface.Canvas;

					canvas.Clear(_backgroundColor);
					if (DisplaySetting.Style.Background.IsEnabled) {
						DrawSegments(canvas, (i, c, p) => DrawFullSegment(c, p));
					}
					if (DisplaySetting.Style.OuterGlow.IsEnabled) {
						DrawSegments(canvas, (i, c, p) => DrawSegment(RasterizeLayer.OuterGlow, i, c, p));
					}
					if (DisplaySetting.Style.InnerGlow.IsEnabled) {
						DrawSegments(canvas, (i, c, p) => DrawSegment(RasterizeLayer.InnerGlow, i, c, p));
					}
					if (DisplaySetting.Style.Foreground.IsEnabled)
					{
						DrawSegments(canvas, (i, c, p) => DrawSegment(RasterizeLayer.Foreground, i, c, p));
					}

					// ReSharper disable once CompareOfFloatsByEqualityOperator
					var fps = _call / (_stopwatch.Elapsed.TotalSeconds != 0 ? _stopwatch.Elapsed.TotalSeconds : 1);
					canvas.DrawText($"FPS: {fps:0}", 0, 10, paint);
					canvas.DrawText($"Frames: {_call++}", 50, 10, paint);
				}
			}
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void DrawSegments(SKCanvas canvas, Action<int, SKCanvas, SKPoint> draw)
		{
			float posX = DisplaySetting.Dim.OuterPadding;
			float posY = DisplaySetting.Dim.OuterPadding;
			for (var j = 0; j < DisplaySetting.NumLines; j++) {
				for (var i = 0; i < DisplaySetting.NumChars; i++) {
					draw(i + DisplaySetting.NumChars * j, canvas, new SKPoint(posX - DisplaySetting.Dim.SegmentPadding, posY - DisplaySetting.Dim.SegmentPadding));
					posX += DisplaySetting.Dim.SvgWidth;
				}
				posX = DisplaySetting.Dim.OuterPadding;
				posY += DisplaySetting.Dim.SvgHeight + DisplaySetting.Dim.LinePadding;
			}
		}

		private void DrawSegment(RasterizeLayer layer, int segmentPosition, SKCanvas canvas, SKPoint canvasPosition)
		{
			var seg = _data[segmentPosition];
			using (var surfacePaint = new SKPaint()) {
				for (var j = 0; j < _res.SegmentSize[DisplaySetting.SegmentType]; j++) {
					var rasterizedSegment = _res.GetRasterized(DisplaySetting.Display, layer, DisplaySetting.SegmentType, j);
					if (((seg >> j) & 0x1) != 0 && rasterizedSegment != null) {
						canvas.DrawSurface(rasterizedSegment, canvasPosition, surfacePaint);
					}
				}
			}
		}

		private void DrawFullSegment(SKCanvas canvas, SKPoint position)
		{
			var segment = _res.GetRasterized(DisplaySetting.Display, RasterizeLayer.Background, DisplaySetting.SegmentType, AlphaNumericResources.FullSegment);
			if (segment != null) {
				canvas.DrawSurface(segment, position);
			}
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