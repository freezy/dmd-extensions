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
		public int NumChars { get; set; }
		public int NumLines { get; set; }
		public SegmentType SegmentType { get; set; }

		private const int SwitchTimeMilliseconds = 150;

		private readonly SKColor _backgroundColor = SKColors.Black;
		private readonly RasterizeStyle _segmentStyle = new RasterizeStyle {
			SkewAngle = -12,
			Background = new RasterizeLayerStyle { Color = new SKColor(0xff, 0xff, 0xff, 0x20), Blur = new SKPoint(7, 7) },
			OuterGlow = new RasterizeLayerStyle { Color = new SKColor(0xb6, 0x58, 0x29, 0x40), Blur = new SKPoint(40, 40), Dilate = new SKPoint(90, 40) },
			InnerGlow = new RasterizeLayerStyle { Color = new SKColor(0xdd, 0x6a, 0x03, 0xa0), Blur = new SKPoint(15, 13), Dilate = new SKPoint(15, 10) },
			Foreground = new RasterizeLayerStyle { Color = new SKColor(0xfb, 0xe6, 0xcb, 0xff), Blur = new SKPoint(2, 2) },
		};
		private RasterizeDimensions _dim;
		private readonly AlphaNumericResources _res = AlphaNumericResources.GetInstance();

		private int _call;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private Dictionary<int, double> _switchPercentage;
		private Dictionary<int, SwitchDirection> _switchDirection;

		private ushort[] _data;

		private long _elapsedMilliseconds = 0;

		public void Init()
		{
			Host.WindowResized.Subscribe(pos => CreateImage((int)pos.Width, (int)pos.Height));

			//for (var i = 0; i < NumChars * NumLines; i++) {
			//	_switchPercentage[i] = 0.0;
			//	_switchDirection[i] = SwitchDirection.Idle;
			//}
			//Host.IgnoreAspectRatio = false;
			//Host.SetDimensions(_alphanumericRenderer.Width, _alphanumericRenderer.Height);
		}

		public void CreateImage(int width, int height)
		{
			Logger.Debug("Creating image...");
			if (!_res.SvgsLoaded) {
				Logger.Debug("Segments unavailable, waiting...");
				_res.Loaded[SegmentType].Take(1).Subscribe(segments => {
					Logger.Debug("Got segments, setting up shit");
					_dim = new RasterizeDimensions(_res.GetSvgSize(SegmentType), width, height, NumChars, NumLines, _segmentStyle);
					Host.SetDimensions(_dim.CanvasWidth, _dim.CanvasHeight);
					_res.Rasterize(SegmentType, _dim, _segmentStyle);
					SetBitmap(new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent));
				});

			} else {
				Logger.Debug("Segments available, let's go!");
				_dim = new RasterizeDimensions(_res.GetSvgSize(SegmentType), width, height, NumChars, NumLines, _segmentStyle);
				Host.SetDimensions(_dim.CanvasWidth, _dim.CanvasHeight);
				_res.Rasterize(SegmentType, _dim, _segmentStyle);
				SetBitmap(new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent));
			}
		}

		public void UpdateData(ushort[] data)
		{
			if (_switchDirection == null) {
				_switchDirection = new Dictionary<int, SwitchDirection>();
			}

			for (var i = 0; i < NumChars * NumLines; i++) {
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
				using (var paint = new SKPaint { Color = SKColors.White, TextSize = 10 }) {
					var canvas = surface.Canvas;

					canvas.Clear(_backgroundColor);
					DrawSegments(canvas, (i, c, p) => DrawFullSegment(c, p));
					DrawSegments(canvas, (i, c, p) => DrawSegment(RasterizeLayer.OuterGlow, i, c, p));
					DrawSegments(canvas, (i, c, p) => DrawSegment(RasterizeLayer.InnerGlow, i, c, p));
					DrawSegments(canvas, (i, c, p) => DrawSegment(RasterizeLayer.Foreground, i, c, p));

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
			float posX = _dim.OuterPadding;
			float posY = _dim.OuterPadding;
			for (var j = 0; j < NumLines; j++) {
				for (var i = 0; i < NumChars; i++) {
					draw(i + NumChars * j, canvas, new SKPoint(posX - _dim.SegmentPadding, posY - _dim.SegmentPadding));
					posX += _dim.SvgWidth;
				}
				posX = _dim.OuterPadding;
				posY += _dim.SvgHeight + _dim.LinePadding;
			}
		}

		private void DrawSegment(RasterizeLayer layer, int segmentPosition, SKCanvas canvas, SKPoint canvasPosition)
		{
			var seg = _data[segmentPosition];
			using (var surfacePaint = new SKPaint()) {
				// todo change 16 depending on segment type
				for (var j = 0; j < 16; j++) {
					var rasterizedSegment = _res.GetRasterized(layer, j);
					if (((seg >> j) & 0x1) != 0 && rasterizedSegment != null) {
						//if (rasterizedSegment.Canvas.DeviceClipBounds.Width != _dim.SvgInfo.Width) {
						//	rasterizedSegment.Canvas.Scale(_dim.SvgInfo.Width / (float)rasterizedSegment.Canvas.DeviceClipBounds.Width);
						//}
						canvas.DrawSurface(rasterizedSegment, canvasPosition, surfacePaint);
					}
				}
			}
		}

		private void DrawFullSegment(SKCanvas canvas, SKPoint position)
		{
			var segment = _res.GetRasterized(RasterizeLayer.Background, AlphaNumericResources.Full);
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
			for (var i = 0; i < NumChars * NumLines; i++) {
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