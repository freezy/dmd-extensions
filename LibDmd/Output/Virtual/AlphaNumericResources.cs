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

		private readonly Dictionary<RasterizeLayer, Dictionary<int, SKSurface>> _rasterized = new Dictionary<RasterizeLayer, Dictionary<int, SKSurface>> {
			{ RasterizeLayer.OuterGlow, new Dictionary<int, SKSurface>() },
			{ RasterizeLayer.InnerGlow, new Dictionary<int, SKSurface>() },
			{ RasterizeLayer.Foreground, new Dictionary<int, SKSurface>() },
			{ RasterizeLayer.Background, new Dictionary<int, SKSurface>() }
		};
		private readonly Dictionary<RasterizeLayer, Dictionary<int, SKSurface>> _rasterizedPrev = new Dictionary<RasterizeLayer, Dictionary<int, SKSurface>> {
			{ RasterizeLayer.OuterGlow, new Dictionary<int, SKSurface>() },
			{ RasterizeLayer.InnerGlow, new Dictionary<int, SKSurface>() },
			{ RasterizeLayer.Foreground, new Dictionary<int, SKSurface>() },
			{ RasterizeLayer.Background, new Dictionary<int, SKSurface>() }
		};

		public static AlphaNumericResources GetInstance()
		{
			return _instance ?? (_instance = new AlphaNumericResources());
		}

		public SKSurface GetRasterized(RasterizeLayer layer, int segment)
		{
			if (_rasterized[layer].ContainsKey(segment)) {
				return _rasterized[layer][segment];
			}
			if (_rasterizedPrev[layer].ContainsKey(segment)) {
				return _rasterizedPrev[layer][segment];
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
		private RasterizeDimensions _rasterizingDim;
		private RasterizeDimensions _rasterizedDim;

		public void Rasterize(SegmentType type, RasterizeDimensions dim, RasterizeStyle style)
		{
			if (_rasterizedDim != null && _rasterizedDim.Equals(dim)) {
				Logger.Info("Already rasterized, aborting.");
				return;
			}
			if (_rasterizingDim != null && _rasterizingDim.Equals(dim)) {
				Logger.Info("Rasterization in progress, aborting.");
				return;
			}
			_rasterizingDim = dim;

			// block the very first time
//			if (_rasterized[RasterizeLayer.Foreground].Keys.Count == 0) {
				Logger.Info("Rasterizing synchronously...");
				RasterizeSync(type, dim, style);
				_rasterizingDim = null;
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
							if (_rasterizedPrev[RasterizeLayer.Foreground].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLayer.Foreground][i]?.Dispose();
							}
							if (_rasterized[RasterizeLayer.Foreground].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLayer.Foreground][i] = _rasterized[RasterizeLayer.Foreground][i];
							}
							_rasterized[RasterizeLayer.Foreground][i] = RasterizeSegment(source[i].Picture, dim, style, foregroundPaint);

							// inner glow
							if (_rasterizedPrev[RasterizeLayer.InnerGlow].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLayer.InnerGlow][i]?.Dispose();
							}
							if (_rasterized[RasterizeLayer.InnerGlow].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLayer.InnerGlow][i] = _rasterized[RasterizeLayer.InnerGlow][i];
							}
							_rasterized[RasterizeLayer.InnerGlow][i] = RasterizeSegment(source[i].Picture, dim, style, innerGlowPaint);

							// outer glow
							if (_rasterizedPrev[RasterizeLayer.OuterGlow].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLayer.OuterGlow][i]?.Dispose();
							}
							if (_rasterized[RasterizeLayer.OuterGlow].ContainsKey(i)) {
								_rasterizedPrev[RasterizeLayer.OuterGlow][i] = _rasterized[RasterizeLayer.OuterGlow][i];
							}
							_rasterized[RasterizeLayer.OuterGlow][i] = RasterizeSegment(source[i].Picture, dim, style, outerGlowPaint);

							if (!_rasterizingDim.Equals(dim)) {
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
				
				if (_rasterizedPrev[RasterizeLayer.Background].ContainsKey(Full)) {
					_rasterizedPrev[RasterizeLayer.Background][Full]?.Dispose();
				}
				if (_rasterized[RasterizeLayer.Background].ContainsKey(Full)) {
					_rasterizedPrev[RasterizeLayer.Background][Full] = _rasterized[RasterizeLayer.Background][Full];
				}
				_rasterized[RasterizeLayer.Background][Full] = RasterizeSegment(source[Full].Picture, dim, style, segmentUnlitPaint);
			}

			_rasterizedDim = dim;
			Logger.Info("Rasterization done.");
		}
	}

	public enum SegmentType
	{
		Alphanumeric, Numeric
	}

	enum RasterizeLayer
	{
		OuterGlow, InnerGlow, Foreground, Background
	}

	class RasterizeDimensions : IEquatable<RasterizeDimensions>
	{
		public int NumChars { get; set; }
		public int NumLines { get; set; }

		public int CanvasWidth { get; }
		public int CanvasHeight { get; }

		public float SvgWidth { get; }
		public float SvgHeight { get; }
		public float SvgScale { get; }
		public SKImageInfo SvgInfo { get; }

		public int OuterPadding { get; }
		public int SegmentPadding { get; }
		public int LinePadding { get; }

		public float TranslateX => _svgSkewedWidth - SvgWidth + SegmentPadding;
		public float TranslateY => SegmentPadding;

		public SKMatrix SvgMatrix;

		public float LinePaddingPercentage { get; set; } = 0.2f;
		public float OuterPaddingPercentage { get; set; } = 0.03f;
		public float SegmentPaddingPercentage { get; set; } = 0.3f;

		private readonly float _svgSkewedWidth;

		public RasterizeDimensions(SKRect svgSize, int width, int height, int numChars, int numLines, RasterizeStyle style)
		{
			NumChars = numChars;
			NumLines = numLines;

			var skewedFactor = SkewedWidth(svgSize.Width, svgSize.Height, style.SkewAngle) / svgSize.Width;
			OuterPadding = (int)Math.Round(OuterPaddingPercentage * width);
			SvgWidth = (width - 2 * OuterPadding) / (NumChars - 1 + skewedFactor);
			SvgScale = SvgWidth / svgSize.Width;
			SvgHeight = svgSize.Height * SvgScale;
			LinePadding = (int)Math.Round(SvgHeight * LinePaddingPercentage);
			SvgMatrix = SKMatrix.MakeScale(SvgScale, SvgScale);
			_svgSkewedWidth = SkewedWidth(SvgWidth, SvgHeight, style.SkewAngle);
			SegmentPadding = (int)Math.Round(Math.Sqrt(SvgWidth * SvgWidth + SvgHeight * SvgHeight) * SegmentPaddingPercentage);
			SvgInfo = new SKImageInfo((int)(_svgSkewedWidth + 2 * SegmentPadding), (int)(SvgHeight + 2 * SegmentPadding));
			CanvasWidth = width;
			CanvasHeight = (int)Math.Round(OuterPadding * 2 + NumLines * SvgHeight + (NumLines - 1) * LinePadding);
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
			return SvgWidth.Equals(other.SvgWidth);
		}
	}

	class RasterizeStyle
	{
		public float SkewAngle { get; set; }
		public RasterizeLayerStyle Foreground { get; set; }
		public RasterizeLayerStyle InnerGlow { get; set; }
		public RasterizeLayerStyle OuterGlow { get; set; }
		public RasterizeLayerStyle Background { get; set; }

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

	class RasterizeLayerStyle
	{
		public SKColor Color { get; set; }
		public SKPoint Blur { get; set; }
		public SKPoint Dilate { get; set; }

		public RasterizeLayerStyle Scale(RasterizeDimensions dim)
		{
			return new RasterizeLayerStyle {
				Color = Color,
				Blur = new SKPoint(dim.SvgScale * Blur.X, dim.SvgScale * Blur.Y),
				Dilate = new SKPoint((float) Math.Round(dim.SvgScale * Dilate.X), (float) Math.Round(dim.SvgScale * Dilate.Y))
			};
		}
	}
}
