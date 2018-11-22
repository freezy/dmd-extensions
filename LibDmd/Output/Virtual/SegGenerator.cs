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

		private readonly SKColor _backgroundColor = SKColors.Black;
		private readonly SKColor _segmentGlowColorOuter = SKColors.Red; // new SKColor(0x8e, 0x51, 0x1d, 0x80);
		private readonly SKColor _segmentGlowColorInner = SKColors.Green; // new SKColor(0xdd, 0x6a, 0x03, 0x80);
		private readonly SKColor _segmentForegroundColor = SKColors.White; // new SKColor(0xfb, 0xd1, 0x9b, 0xff);
		private readonly SKColor _segmentUnlitBackgroundColor = new SKColor(0xff, 0xff, 0xff, 0x1d);

		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private int _call;
		private readonly Stopwatch _stopwatch = new Stopwatch();

		private readonly SkiaSharp.Extended.Svg.SKSvg _fullSvg = new SkiaSharp.Extended.Svg.SKSvg();
		private readonly Dictionary<int, SkiaSharp.Extended.Svg.SKSvg> _segments = new Dictionary<int, SkiaSharp.Extended.Svg.SKSvg>();

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

		private const int SkewAngle = -12;
		private const int NumSegments = 20;
		private const int NumLines = 2;
		private const int Padding = 30;
		private const int SegmentPaddingFactor = 200;

		public SegGenerator()
		{
			LoadSvgs();
		}

		public WriteableBitmap CreateImage(int width, int height)
		{
			SetupDimensions(width, height);
			RasterizeSegments();
			return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
		}

		public void UpdateFrame(AlphaNumericFrame frame)
		{
			_frame = frame;
		}

		private void DrawSegment(int num, SKCanvas canvas, SKPoint position)
		{
			var seg = _frame.SegmentData[num];
			for (var j = 0; j < 16; j++) {
				if (((seg >> j) & 0x1) != 0) {
					canvas.DrawSurface(_segmentsForegroundRasterized[j], position);
				}
			}
		}

		private void DrawSegmentBackgroundEffect(int num, SKCanvas canvas, SKPoint position)
		{
			var seg = _frame.SegmentData[num];
			for (var j = 0; j < 16; j++) {
				if (((seg >> j) & 0x1) != 0) {
					canvas.DrawSurface(_segmentsOuterGlowRasterized[j], position);
				}
			}
		}

		public void DrawImage(WriteableBitmap writeableBitmap)
		{
			if (_frame == null || _segmentsForegroundRasterized.Count == 0) {
				return;
			}
			if (_call == 0) {
				_stopwatch.Start();
			}

			var width = (int) writeableBitmap.Width;
			var height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			using (var surface = SKSurface.Create(width, height, SKColorType.Bgra8888, SKAlphaType.Premul,
				writeableBitmap.BackBuffer, width * 4)) {

				var canvas = surface.Canvas;
				var paint = new SKPaint { Color = SKColors.White, TextSize = 10 };

				canvas.Clear(_backgroundColor);
				DrawBackgroundSegments(canvas);
				DrawSegmentsBackgroundEffect(canvas);
				DrawSegments(canvas);


				// ReSharper disable once CompareOfFloatsByEqualityOperator
				var fps = _call / (_stopwatch.Elapsed.TotalSeconds != 0 ? _stopwatch.Elapsed.TotalSeconds : 1);
				canvas.DrawText($"FPS: {fps:0}", 0, 10, paint);
				canvas.DrawText($"Frames: {this._call++}", 50, 10, paint);
			}

			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void DrawBackgroundSegments(SKCanvas canvas)
		{
			float transX = 0;
			float transY = 0;
			for (var j = 0; j < NumLines; j++) {
				for (var i = 0; i < NumSegments; i++) {
					canvas.DrawSurface(_fullSvgRasterized, new SKPoint(transX, transY));
					transX += _svgWidth;
				}
				transX = 0;
				transY += _svgHeight + 10;
			}
		}

		private void DrawSegmentsBackgroundEffect(SKCanvas canvas)
		{
			float posX = 0;
			float posY = 0;
			for (var j = 0; j < NumLines; j++) {
				for (var i = 0; i < NumSegments; i++) {
					DrawSegmentBackgroundEffect(i + 20 * j, canvas, new SKPoint(posX, posY));
					posX += _svgWidth;
				}
				posX = 0;
				posY += _svgHeight + 10;
			}
		}

		private void DrawSegments(SKCanvas canvas)
		{
			float posX = 0;
			float posY = 0;
			for (var j = 0; j < NumLines; j++) {
				for (var i = 0; i < NumSegments; i++) {
					DrawSegment(i + 20 * j, canvas, new SKPoint(posX, posY));
					posX += _svgWidth;
				}
				posX = 0;
				posY += _svgHeight + 10;
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

		private readonly SKPoint _outerDilateFactor = new SKPoint(90, 40);
		private readonly SKPoint _outerBlurFactor = new SKPoint(40, 40);
		private readonly SKPoint _innerDilateFactor = new SKPoint(15, 10);
		private readonly SKPoint _innerBlurFactor = new SKPoint(12, 10);

		private void RasterizeSegments()
		{
			Logger.Info("Rasterizing alphanumeric segments with scale = {0}, segment size = {1}x{2}, outer padding = {3}, segment canvas padding = {4}", _svgScale, _svgWidth, _svgHeight, Padding, SegmentPaddingFactor * _svgScale);
			using (var glowOuterPaint = new SKPaint()) {
				using (var glowInnerPaint = new SKPaint()) {
					using (var foregroundPaint = new SKPaint()) {

						glowOuterPaint.ColorFilter = SKColorFilter.CreateBlendMode(_segmentGlowColorOuter, SKBlendMode.SrcIn);
						//glowOuterPaint.ImageFilter = SKImageFilter.CreateBlur(15, 15, SKImageFilter.CreateDilate(8, 5));
						glowOuterPaint.ImageFilter = SKImageFilter.CreateBlur(scaleFactor(_outerBlurFactor.X), scaleFactor(_outerBlurFactor.Y), 
							SKImageFilter.CreateDilate(scaleFactor(_outerDilateFactor.X), scaleFactor(_outerDilateFactor.Y)));
						
						glowInnerPaint.ColorFilter = SKColorFilter.CreateBlendMode(_segmentGlowColorInner, SKBlendMode.SrcIn);
						//glowInnerPaint.ImageFilter = SKImageFilter.CreateBlur(6, 6, SKImageFilter.CreateDilate(6, 6));
						glowInnerPaint.ImageFilter = SKImageFilter.CreateBlur(scaleFactor(_innerBlurFactor.X), scaleFactor(_innerBlurFactor.Y), 
							SKImageFilter.CreateDilate(scaleFactor(_innerDilateFactor.X), scaleFactor(_innerDilateFactor.Y)));

						foregroundPaint.ColorFilter = SKColorFilter.CreateBlendMode(_segmentForegroundColor, SKBlendMode.SrcIn);
						//foregroundPaint.ImageFilter = SKImageFilter.CreateBlur(1, 1);

						foreach (var i in _segments.Keys) {
							if (_segmentsForegroundRasterized.ContainsKey(i)) {
								_segmentsForegroundRasterized[i].Dispose();
							}
							if (_segmentsOuterGlowRasterized.ContainsKey(i)) {
								_segmentsOuterGlowRasterized[i].Dispose();
							}
							_segmentsOuterGlowRasterized[i] = RasterizeSegment(_segments[i].Picture, glowOuterPaint, glowInnerPaint);
							_segmentsForegroundRasterized[i] = RasterizeSegment(_segments[i].Picture, foregroundPaint);
							//_segmentsForegroundRasterized[i] = RasterizeSegment(_segments[i].Picture, glowOuterPaint, glowInnerPaint, foregroundPaint);
						}
					}
				}
			}
			

			using (var backgroundPaint = new SKPaint()) {
				backgroundPaint.ColorFilter = SKColorFilter.CreateBlendMode(_segmentUnlitBackgroundColor, SKBlendMode.SrcIn);
				//backgroundPaint.ImageFilter = SKImageFilter.CreateBlur(3, 3);
				_fullSvgRasterized?.Dispose();
				_fullSvgRasterized = RasterizeSegment(_fullSvg.Picture, backgroundPaint);
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
}