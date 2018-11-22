using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NLog;
using SkiaSharp;

namespace LibDmd.Output.Virtual
{

	class SegGenerator
	{
		private const int SkewAngle = -12;
		private const int NumSegments = 20;
		private const int NumLines = 2;
		private const int Padding = 30;
		private const float SwitchTimeMilliseconds = 200;
		private const int SegmentPaddingFactor = 200;

		private readonly SKColor _backgroundColor = SKColors.Black;
		private readonly SKColor _segmentOuterGlowColor = new SKColor(0xb6, 0x58, 0x29, 0x40); // b65829
		private readonly SKColor _segmentInnerGlowColor = new SKColor(0xdd, 0x6a, 0x03, 0xa0);
		private readonly SKColor _segmentForegroundColor = new SKColor(0xfb, 0xe6, 0xcb, 0xff); // fbe6cb
		private readonly SKColor _segmentUnlitBackgroundColor = new SKColor(0xff, 0xff, 0xff, 0x1d);

		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private int _call;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private readonly SkiaSharp.Extended.Svg.SKSvg _fullSvg = new SkiaSharp.Extended.Svg.SKSvg();
		private readonly Dictionary<int, SkiaSharp.Extended.Svg.SKSvg> _segments = new Dictionary<int, SkiaSharp.Extended.Svg.SKSvg>();
		private readonly Dictionary<int, double> _switchPercentage = new Dictionary<int, double>(NumSegments * NumLines);
		private readonly Dictionary<int, SwitchDirection> _switchDirection = new Dictionary<int, SwitchDirection>(NumSegments * NumLines);

		private readonly Dictionary<int, SKSurface> _segmentsOuterGlowRasterized = new Dictionary<int, SKSurface>();
		private readonly Dictionary<int, SKSurface> _segmentsInnerGlowRasterized = new Dictionary<int, SKSurface>();
		private readonly Dictionary<int, SKSurface> _segmentsForegroundRasterized = new Dictionary<int, SKSurface>();
		private SKSurface _fullSvgRasterized;

		private AlphaNumericFrame _frame;

		private float _svgWidth;
		private float _svgHeight;
		private float _svgScale;
		private SKMatrix _svgMatrix;
		private SKImageInfo _svgInfo;

		private long _elapsedMilliseconds = 0;

		private readonly SKPoint _unlitBlurFactor = new SKPoint(7, 7);
		private readonly SKPoint _outerDilateFactor = new SKPoint(90, 40);
		private readonly SKPoint _outerBlurFactor = new SKPoint(40, 40);
		private readonly SKPoint _innerDilateFactor = new SKPoint(15, 10);
		private readonly SKPoint _innerBlurFactor = new SKPoint(15, 13);
		private readonly SKPoint _foregroundBlurFactor = new SKPoint(2, 2);

		public SegGenerator()
		{
			LoadSvgs();
			for (var i = 0; i < NumSegments * NumLines; i++) {
				_switchPercentage[i] = 0;
				_switchDirection[i] = SwitchDirection.Idle;
			}
		}

		public WriteableBitmap CreateImage(int width, int height)
		{
			SetupDimensions(width, height);
			RasterizeSegments();
			return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
		}

		public void UpdateFrame(AlphaNumericFrame frame)
		{
			for (var i = 0; i < NumSegments * NumLines; i++) {
				var onBefore = _frame != null && _frame.SegmentData[i] != 0;
				var onAfter = frame.SegmentData[i] != 0;
				if (onBefore == onAfter) {
					_switchDirection[i] = SwitchDirection.Idle;
				} else {
					_switchDirection[i] = onBefore ? SwitchDirection.Off : SwitchDirection.On;
				}
			}
			_frame = frame;
		}

		public void DrawImage(WriteableBitmap writeableBitmap)
		{
			if (_frame == null || _segmentsForegroundRasterized.Count == 0) {
				return;
			}

			UpdateSwitchStatus(_stopwatch.ElapsedMilliseconds - _elapsedMilliseconds);

			if (_call == 0) {
				_stopwatch.Start();
			} else {
				_elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
			}

			var width = (int) writeableBitmap.Width;
			var height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			using (var surface = SKSurface.Create(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, writeableBitmap.BackBuffer, width * 4)) {

				var canvas = surface.Canvas;
				var paint = new SKPaint { Color = SKColors.White, TextSize = 10 };

				canvas.Clear(_backgroundColor);
				DrawSegments(canvas, (i, c, p) => c.DrawSurface(_fullSvgRasterized, p));
				DrawSegments(canvas, (i, c, p) => DrawSegment(_segmentsOuterGlowRasterized, i, c, p));
				DrawSegments(canvas, (i, c, p) => DrawSegment(_segmentsInnerGlowRasterized, i, c, p));
				DrawSegments(canvas, (i, c, p) => DrawSegment(_segmentsForegroundRasterized, i, c, p));

				// ReSharper disable once CompareOfFloatsByEqualityOperator
				var fps = _call / (_stopwatch.Elapsed.TotalSeconds != 0 ? _stopwatch.Elapsed.TotalSeconds : 1);
				canvas.DrawText($"FPS: {fps:0}", 0, 10, paint);
				canvas.DrawText($"Frames: {_call++}", 50, 10, paint);
			}

			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void UpdateSwitchStatus(long elapsedMillisecondsSinceLastFrame)
		{
			var elapsedPercentage = elapsedMillisecondsSinceLastFrame / SwitchTimeMilliseconds;
			for (var i = 0; i < NumSegments; i++) {
				switch (_switchDirection[i]) {
					case SwitchDirection.Idle:
						continue;
					case SwitchDirection.On:
						_switchPercentage[i] = Math.Min(1, _switchPercentage[i] + elapsedPercentage);
						break;
					case SwitchDirection.Off:
						_switchPercentage[i] = Math.Max(0, _switchPercentage[i] - elapsedPercentage);
						break;
				}
				if (_switchPercentage[i] >= 1 || _switchPercentage[i] <= 0) {
					_switchDirection[i] = SwitchDirection.Idle;
				}
			}
		}

		private void DrawSegments(SKCanvas canvas, Action<int, SKCanvas, SKPoint> draw)
		{
			float posX = 0;
			float posY = 0;
			for (var j = 0; j < NumLines; j++) {
				for (var i = 0; i < NumSegments; i++) {
					draw(i + 20 * j, canvas, new SKPoint(posX, posY));
					posX += _svgWidth;
				}
				posX = 0;
				posY += _svgHeight + 10;
			}
		}

		private void DrawSegment(Dictionary<int, SKSurface> source, int num, SKCanvas canvas, SKPoint position)
		{
			var seg = _frame.SegmentData[num];
			using (var surfacePaint = new SKPaint()) {
				surfacePaint.Color = surfacePaint.Color.WithAlpha((byte)(0xFF * _switchPercentage[num]));
				for (var j = 0; j < 16; j++) {
					if (((seg >> j) & 0x1) != 0) {
						canvas.DrawSurface(source[j], position, surfacePaint);
					}
				}
			}
		}

		private void SetupDimensions(int width, int height)
		{
			var svgSize = _segments[0].Picture.CullRect;
			var skewedFactor = SkewedWidth(svgSize.Width, svgSize.Height) / svgSize.Width;
			_svgWidth = (width - (2f * Padding)) / (NumSegments - 1 + skewedFactor);
			_svgScale = _svgWidth / svgSize.Width;
			_svgHeight = svgSize.Height * _svgScale;
			_svgMatrix = SKMatrix.MakeScale(_svgScale, _svgScale);
			var skewedWidth = SkewedWidth(_svgWidth, _svgHeight);
			_svgInfo = new SKImageInfo((int)(skewedWidth + 2 * SegmentPaddingFactor * _svgScale), (int)(_svgHeight + 2 * SegmentPaddingFactor * _svgScale));
		}

		private void RasterizeSegments()
		{
			Logger.Info("Rasterizing alphanumeric segments with scale = {0}, segment size = {1}x{2}, outer padding = {3}, segment canvas padding = {4}", _svgScale, _svgWidth, _svgHeight, Padding, SegmentPaddingFactor * _svgScale);
			using (var outerGlowPaint = new SKPaint()) {
				using (var innerGlowPaint = new SKPaint()) {
					using (var segmentPaint = new SKPaint()) {

						outerGlowPaint.ColorFilter = SKColorFilter.CreateBlendMode(_segmentOuterGlowColor, SKBlendMode.SrcIn);
						outerGlowPaint.ImageFilter = SKImageFilter.CreateBlur(scaleFactor(_outerBlurFactor.X), scaleFactor(_outerBlurFactor.Y), 
							SKImageFilter.CreateDilate(scaleFactor(_outerDilateFactor.X), scaleFactor(_outerDilateFactor.Y)));
						
						innerGlowPaint.ColorFilter = SKColorFilter.CreateBlendMode(_segmentInnerGlowColor, SKBlendMode.SrcIn);
						innerGlowPaint.ImageFilter = SKImageFilter.CreateBlur(scaleFactor(_innerBlurFactor.X), scaleFactor(_innerBlurFactor.Y), 
							SKImageFilter.CreateDilate(scaleFactor(_innerDilateFactor.X), scaleFactor(_innerDilateFactor.Y)));

						segmentPaint.ColorFilter = SKColorFilter.CreateBlendMode(_segmentForegroundColor, SKBlendMode.SrcIn);
						segmentPaint.ImageFilter = SKImageFilter.CreateBlur(scaleFactor(_foregroundBlurFactor.X), scaleFactor(_foregroundBlurFactor.Y));

						foreach (var i in _segments.Keys) {
							if (_segmentsOuterGlowRasterized.ContainsKey(i)) {
								_segmentsOuterGlowRasterized[i].Dispose();
							}
							if (_segmentsInnerGlowRasterized.ContainsKey(i)) {
								_segmentsInnerGlowRasterized[i].Dispose();
							}
							if (_segmentsForegroundRasterized.ContainsKey(i)) {
								_segmentsForegroundRasterized[i].Dispose();
							}
							_segmentsOuterGlowRasterized[i] = RasterizeSegment(_segments[i].Picture, outerGlowPaint);
							_segmentsInnerGlowRasterized[i] = RasterizeSegment(_segments[i].Picture, innerGlowPaint);
							_segmentsForegroundRasterized[i] = RasterizeSegment(_segments[i].Picture, segmentPaint);
						}
					}
				}
			}

			using (var segmentUnlitPaint = new SKPaint()) {
				segmentUnlitPaint.ColorFilter = SKColorFilter.CreateBlendMode(_segmentUnlitBackgroundColor, SKBlendMode.SrcIn);
				segmentUnlitPaint.ImageFilter = SKImageFilter.CreateBlur(scaleFactor(_unlitBlurFactor.X), scaleFactor(_unlitBlurFactor.Y));
				_fullSvgRasterized?.Dispose();
				_fullSvgRasterized = RasterizeSegment(_fullSvg.Picture, segmentUnlitPaint);
			}
		}

		private int scaleFactor(double factor)
		{
			return (int) Math.Round(factor * _svgScale);
		}

		private SKSurface RasterizeSegment(SKPicture segment, params SKPaint[] paints)
		{
			var surface = SKSurface.Create(_svgInfo);
			surface.Canvas.Translate(_svgInfo.Width - _svgWidth - Padding - SegmentPaddingFactor * _svgScale, Padding);
			Skew(surface.Canvas, SkewAngle, 0);
			foreach (var paint in paints) {
				surface.Canvas.DrawPicture(segment, ref _svgMatrix, paint);
			}
			return surface;
		}

		private void LoadSvgs()
		{
			// load svgs from packages resources
			const string prefix = "LibDmd.Output.Virtual.alphanum_thin_inner.";
			//const string prefix = "LibDmd.Output.Virtual.alphanum.";
			var segmentFileNames = new[]
			{
				$"{prefix}00-top.svg",
				$"{prefix}01-top-right.svg",
				$"{prefix}02-bottom-right.svg",
				$"{prefix}03-bottom.svg",
				$"{prefix}04-bottom-left.svg",
				$"{prefix}05-top-left.svg",
				$"{prefix}06-middle-left.svg",
				$"{prefix}07-comma.svg",
				$"{prefix}08-diag-top-left.svg",
				$"{prefix}09-center-top.svg",
				$"{prefix}10-diag-top-right.svg",
				$"{prefix}11-middle-right.svg",
				$"{prefix}12-diag-bottom-right.svg",
				$"{prefix}13-center-bottom.svg",
				$"{prefix}14-diag-bottom-left.svg",
				$"{prefix}15-dot.svg",
			};
			Logger.Info("Loading segment SVGs...");
			for (var i = 0; i < segmentFileNames.Length; i++) {
				var svg = new SkiaSharp.Extended.Svg.SKSvg();
				svg.Load(_assembly.GetManifestResourceStream(segmentFileNames[i]));
				_segments.Add(i, svg);
			}
			_fullSvg.Load(_assembly.GetManifestResourceStream($"{prefix}full.svg"));
		}

		private static void Skew(SKCanvas canvas, double xDegrees, double yDegrees)
		{
			canvas.Skew((float)Math.Tan(Math.PI * xDegrees / 180), (float)Math.Tan(Math.PI * yDegrees / 180));
		}

		private static float SkewedWidth(float width, float height)
		{
			if (SkewAngle == 0) {
				return width;
			}
			var skew = (float)Math.Tan(Math.PI * SkewAngle / 180);
			return width + Math.Abs(skew * height);
		}
	}

	internal enum SwitchDirection
	{
		On, Off, Idle
	}
}