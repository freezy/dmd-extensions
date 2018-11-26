using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using SkiaSharp;
using WebSocketSharp;
using Logger = NLog.Logger;
using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

namespace LibDmd.Output.Virtual
{
	class AlphaNumericResources
	{
		public static int Full = 99;
		public Dictionary<SegmentType, ISubject<Dictionary<int, SKSvg>>> Loaded = new Dictionary<SegmentType, ISubject<Dictionary<int, SKSvg>>> {
			{ SegmentType.Alphanumeric, new Subject<Dictionary<int, SKSvg>>()},
			{ SegmentType.Numeric, new Subject<Dictionary<int, SKSvg>>() }
		};
		public bool SvgsLoaded { get; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static AlphaNumericResources _instance;
		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

		private readonly Dictionary<SegmentType, Dictionary<int, SKSvg>> _svgs = new Dictionary<SegmentType, Dictionary<int, SKSvg>> {
			{ SegmentType.Alphanumeric, new Dictionary<int, SKSvg>() },
			{ SegmentType.Numeric, new Dictionary<int, SKSvg>() }
		};

		private readonly Dictionary<RasterizeLevel, Dictionary<int, SKSurface>> _rasterized = new Dictionary<RasterizeLevel, Dictionary<int, SKSurface>> {
			{ RasterizeLevel.OuterGlow, new Dictionary<int, SKSurface>() },
			{ RasterizeLevel.InnerGlow, new Dictionary<int, SKSurface>() },
			{ RasterizeLevel.Foreground, new Dictionary<int, SKSurface>() },
			{ RasterizeLevel.Background, new Dictionary<int, SKSurface>() }
		};
		private readonly Dictionary<RasterizeLevel, Dictionary<int, SKSurface>> _rasterizedPrev = new Dictionary<RasterizeLevel, Dictionary<int, SKSurface>> {
			{ RasterizeLevel.OuterGlow, new Dictionary<int, SKSurface>() },
			{ RasterizeLevel.InnerGlow, new Dictionary<int, SKSurface>() },
			{ RasterizeLevel.Foreground, new Dictionary<int, SKSurface>() },
			{ RasterizeLevel.Background, new Dictionary<int, SKSurface>() }
		};

		public static AlphaNumericResources GetInstance()
		{
			return _instance ?? (_instance = new AlphaNumericResources());
		}

		public SKSurface GetRasterized(RasterizeLevel level, int segment)
		{
			if (_rasterized[level].ContainsKey(segment)) {
				return _rasterized[level][segment];
			}
			if (_rasterizedPrev[level].ContainsKey(segment)) {
				return _rasterizedPrev[level][segment];
			}
			return null;
		}

		public SKRect GetSvgSize(SegmentType type)
		{
			return _svgs[type][Full].Picture.CullRect;
		}

		private AlphaNumericResources()
		{
			LoadAlphaNumeric();
			LoadNumeric();
			SvgsLoaded = true;
			Logger.Info("All SVGs loaded.");
		}

		private void LoadAlphaNumeric()
		{
			const string prefix = "LibDmd.Output.Virtual.alphanum.";
			var segmentFileNames = new[] {
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
			Logger.Info("Loading alphanumeric SVGs...");
			Load(segmentFileNames, prefix, SegmentType.Alphanumeric);
		}

		private void LoadNumeric()
		{
			const string prefix = "LibDmd.Output.Virtual.numeric.";
			var segmentFileNames = new[] {
				$"{prefix}00-top.svg",
				$"{prefix}01-top-right.svg",
				$"{prefix}02-bottom-right.svg",
				$"{prefix}03-bottom.svg",
				$"{prefix}04-bottom-left.svg",
				$"{prefix}05-top-left.svg",
				$"{prefix}06-middle.svg",
				$"{prefix}07-comma.svg",
			};
			Logger.Info("Loading numeric SVGs...");
			Load(segmentFileNames, prefix, SegmentType.Numeric);
		}

		private void Load(string[] fileNames, string prefix, SegmentType type)
		{
			for (var i = 0; i < fileNames.Length; i++) {
				var svg = new SKSvg();
				svg.Load(_assembly.GetManifestResourceStream(fileNames[i]));
				_svgs[type].Add(i, svg);
			}
			var full = new SKSvg();
			full.Load(_assembly.GetManifestResourceStream($"{prefix}full.svg"));
			_svgs[type].Add(Full, full);
			Loaded[type].OnNext(_svgs[type]);
			Loaded[type] = new BehaviorSubject<Dictionary<int, SKSvg>>(_svgs[type]);
		}

		private SKSurface RasterizeSegment(SKPicture segment, RasterizeDimensions dim, RasterizeStyle style, params SKPaint[] paints)
		{
			if (dim.SvgWidth <= 0 || dim.SvgWidth <= 0) {
				Logger.Warn("Skipping rasterizing of segment {0}", style);
				return null;
			}
			var surface = SKSurface.Create(dim.SvgInfo);
			surface.Canvas.Translate(dim.TranslateX, dim.TranslateY);
			Skew(surface.Canvas, style.SkewAngle, 0);
			foreach (var paint in paints) {
				surface.Canvas.DrawPicture(segment, ref dim.SvgMatrix, paint);
			}
			return surface;
		}

		private static void Skew(SKCanvas canvas, double xDegrees, double yDegrees)
		{
			canvas.Skew((float)Math.Tan(Math.PI * xDegrees / 180), (float)Math.Tan(Math.PI * yDegrees / 180));
		}


		private Thread _rasterizeThread;
		private RasterizeDimensions _rasterizing;

		public void Rasterize(SegmentType type, RasterizeDimensions dim, RasterizeStyle style)
		{
			if (_rasterizing != null && _rasterizing.Equals(dim)) {
				Logger.Info("Rasterization in progress, aborting.");
				return;
			}
			_rasterizing = dim;

			// block the very first time
//			if (_rasterized[RasterizeLevel.Foreground].Keys.Count == 0) {
				Logger.Info("Rasterizing synchronously...");
				RasterizeSync(type, dim, style);
				_rasterizing = null;
//			} else {
//				Logger.Info("Rasterizing asynchronously...");
//				_rasterizeThread = new Thread(() => RasterizeSync(type, dim, style));
//				_rasterizeThread.Start();
//			}
		}

		private void RasterizeSync(SegmentType type, RasterizeDimensions dim, RasterizeStyle style)
		{
			var scaledStyle = style.Scale(dim);
			var source = _svgs[type];

			Logger.Info("Rasterizing alphanumeric segments with scale = {0}, segment size = {1}x{2}", dim.SvgScale, dim.SvgWidth, dim.SvgHeight);
			using (var outerGlowPaint = new SKPaint()) {
				using (var innerGlowPaint = new SKPaint()) {
					using (var foregroundPaint = new SKPaint()) {

						outerGlowPaint.ColorFilter = SKColorFilter.CreateBlendMode(style.OuterGlow.Color, SKBlendMode.SrcIn);
						outerGlowPaint.ImageFilter = SKImageFilter.CreateBlur(scaledStyle.OuterGlow.Blur.X, scaledStyle.OuterGlow.Blur.Y,
							SKImageFilter.CreateDilate((int)scaledStyle.OuterGlow.Dilate.X, (int)scaledStyle.OuterGlow.Dilate.X));

						innerGlowPaint.ColorFilter = SKColorFilter.CreateBlendMode(style.InnerGlow.Color, SKBlendMode.SrcIn);
						innerGlowPaint.ImageFilter = SKImageFilter.CreateBlur(scaledStyle.InnerGlow.Blur.X, scaledStyle.InnerGlow.Blur.Y,
							SKImageFilter.CreateDilate((int)scaledStyle.InnerGlow.Dilate.X, (int)scaledStyle.InnerGlow.Dilate.Y));

						foregroundPaint.ColorFilter = SKColorFilter.CreateBlendMode(style.Foreground.Color, SKBlendMode.SrcIn);
						foregroundPaint.ImageFilter = SKImageFilter.CreateBlur(scaledStyle.Foreground.Blur.X, scaledStyle.Foreground.Blur.Y);

						foreach (var i in source.Keys.Where(i => i != Full)) {

							// foreground
							if (_rasterizedPrev[RasterizeLevel.Foreground].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLevel.Foreground][i]?.Dispose();
							}
							if (_rasterized[RasterizeLevel.Foreground].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLevel.Foreground][i] = _rasterized[RasterizeLevel.Foreground][i];
							}
							_rasterized[RasterizeLevel.Foreground][i] = RasterizeSegment(source[i].Picture, dim, style, foregroundPaint);

							// inner glow
							if (_rasterizedPrev[RasterizeLevel.InnerGlow].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLevel.InnerGlow][i]?.Dispose();
							}
							if (_rasterized[RasterizeLevel.InnerGlow].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLevel.InnerGlow][i] = _rasterized[RasterizeLevel.InnerGlow][i];
							}
							_rasterized[RasterizeLevel.InnerGlow][i] = RasterizeSegment(source[i].Picture, dim, style, innerGlowPaint);

							// outer glow
							if (_rasterizedPrev[RasterizeLevel.OuterGlow].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLevel.OuterGlow][i]?.Dispose();
							}
							if (_rasterized[RasterizeLevel.OuterGlow].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLevel.OuterGlow][i] = _rasterized[RasterizeLevel.OuterGlow][i];
							}
							_rasterized[RasterizeLevel.OuterGlow][i] = RasterizeSegment(source[i].Picture, dim, style, outerGlowPaint);

							if (!_rasterizing.Equals(dim)) {
								Logger.Warn("Aborting rastering!");
								return;
							}
						}
					}
				}
			}

			// unlit tubes
			using (var segmentUnlitPaint = new SKPaint()) {
				segmentUnlitPaint.ColorFilter = SKColorFilter.CreateBlendMode(style.Background.Color, SKBlendMode.SrcIn);
				segmentUnlitPaint.ImageFilter = SKImageFilter.CreateBlur(scaledStyle.Background.Blur.X, scaledStyle.Background.Blur.Y);
				
				if (_rasterizedPrev[RasterizeLevel.Background].ContainsKey(Full)) {
					_rasterizedPrev[RasterizeLevel.Background][Full]?.Dispose();
				}
				if (_rasterized[RasterizeLevel.Background].ContainsKey(Full)) {
					_rasterizedPrev[RasterizeLevel.Background][Full] = _rasterized[RasterizeLevel.Background][Full];
				}
				_rasterized[RasterizeLevel.Background][Full] = RasterizeSegment(source[Full].Picture, dim, style, segmentUnlitPaint);
			}

			Logger.Info("Rasterization done.");
		}
	}

	public enum SegmentType
	{
		Alphanumeric, Numeric
	}

	enum RasterizeLevel
	{
		OuterGlow, InnerGlow, Foreground, Background
	}

	class RasterizeDimensions : IEquatable<RasterizeDimensions>
	{
		public int NumChars { get; set; }
		public int NumLines { get; set; }

		public float SvgWidth => _svgWidth;
		public float SvgHeight => _svgHeight;
		public float SvgScale => _svgScale;
		public SKImageInfo SvgInfo => _svgInfo;

		public int OuterPadding => _outerPadding;
		public int SegmentPadding => _segmentPadding;
		public int LinePadding => _linePadding;

		public float TranslateX => _svgSkewedWidth - _svgWidth + _segmentPadding;
		public float TranslateY => _segmentPadding;

		public SKMatrix SvgMatrix;

		public float LinePaddingPercentage { get; set; } = 0.2f;
		public float OuterPaddingPercentage { get; set; } = 0.03f;
		public float SegmentPaddingPercentage { get; set; } = 0.3f;

		private int _outerPadding;
		private float _svgWidth;
		private float _svgScale;
		private float _svgHeight;
		private int _linePadding;
		private float _svgSkewedWidth;
		private int _segmentPadding;
		private SKImageInfo _svgInfo;
		private int _canvasWidth;
		private int _canvasHeight;

		public RasterizeDimensions(SKRect svgSize, int width, int height, int numChars, int numLines, RasterizeStyle style)
		{
			NumChars = numChars;
			NumLines = numLines;

			var skewedFactor = SkewedWidth(svgSize.Width, svgSize.Height, style.SkewAngle) / svgSize.Width;
			_outerPadding = (int)Math.Round(OuterPaddingPercentage * width);
			_svgWidth = (width - 2 * _outerPadding) / (NumChars - 1 + skewedFactor);
			_svgScale = _svgWidth / svgSize.Width;
			_svgHeight = svgSize.Height * _svgScale;
			_linePadding = (int)Math.Round(_svgHeight * LinePaddingPercentage);
			SvgMatrix = SKMatrix.MakeScale(_svgScale, _svgScale);
			_svgSkewedWidth = SkewedWidth(_svgWidth, _svgHeight, style.SkewAngle);
			_segmentPadding = (int)Math.Round(Math.Sqrt(_svgWidth * _svgWidth + _svgHeight * _svgHeight) * SegmentPaddingPercentage);
			_svgInfo = new SKImageInfo((int)(_svgSkewedWidth + 2 * _segmentPadding), (int)(_svgHeight + 2 * _segmentPadding));
			_canvasWidth = width;
			_canvasHeight = (int)Math.Round(_outerPadding * 2 + NumLines * _svgHeight + (NumLines - 1) * _linePadding);
		}

		private static float SkewedWidth(float width, float height, float angle)
		{
			var skew = (float)Math.Tan(Math.PI * angle / 180);
			return width + Math.Abs(skew * height);
		}

		public bool Equals(RasterizeDimensions other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return _svgWidth.Equals(other._svgWidth);
		}
	}

	class RasterizeStyle
	{
		public float SkewAngle { get; set; }
		public RasterizeLevelStyle Foreground { get; set; }
		public RasterizeLevelStyle InnerGlow { get; set; }
		public RasterizeLevelStyle OuterGlow { get; set; }
		public RasterizeLevelStyle Background { get; set; }

		public RasterizeStyle Scale(RasterizeDimensions dim)
		{
			return new RasterizeStyle {
				SkewAngle = SkewAngle,
				Foreground = Foreground.Scale(dim),
				InnerGlow = InnerGlow.Scale(dim),
				OuterGlow = OuterGlow.Scale(dim),
				Background = Background.Scale(dim),
			};
		}
	}

	class RasterizeLevelStyle
	{
		public SKColor Color { get; set; }
		public SKPoint Blur { get; set; }
		public SKPoint Dilate { get; set; }

		public RasterizeLevelStyle Scale(RasterizeDimensions dim)
		{
			return new RasterizeLevelStyle {
				Color = Color,
				Blur = new SKPoint(dim.SvgScale * Blur.X, dim.SvgScale * Blur.Y),
				Dilate = new SKPoint((float) Math.Round(dim.SvgScale * Dilate.X), (float) Math.Round(dim.SvgScale * Dilate.Y))
			};
		}
	}
}
