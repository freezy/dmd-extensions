using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using NLog;
using SkiaSharp;
using Logger = NLog.Logger;
using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

namespace LibDmd.Output.Virtual
{
	class AlphaNumericResources
	{
		public static int FullSegment = 99;
		public static int InitialCache = 99;

		public Dictionary<SegmentType, ISubject<Dictionary<int, SKSvg>>> Loaded = new Dictionary<SegmentType, ISubject<Dictionary<int, SKSvg>>> {
			{ SegmentType.Alphanumeric, new Subject<Dictionary<int, SKSvg>>()},
			{ SegmentType.Numeric8, new Subject<Dictionary<int, SKSvg>>() },
			{ SegmentType.Numeric10, new Subject<Dictionary<int, SKSvg>>() }
		};
		public readonly Dictionary<SegmentType, int> SegmentSize = new Dictionary<SegmentType, int> {
			{ SegmentType.Alphanumeric, 16 },
			{ SegmentType.Numeric8, 8 },
			{ SegmentType.Numeric10, 10 },
		};
		public bool SvgsLoaded { get; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static AlphaNumericResources _instance;
		private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

		private readonly Dictionary<SegmentType, Dictionary<int, SKSvg>> _svgs = new Dictionary<SegmentType, Dictionary<int, SKSvg>> {
			{ SegmentType.Alphanumeric, new Dictionary<int, SKSvg>() },
			{ SegmentType.Numeric8, new Dictionary<int, SKSvg>() },
			{ SegmentType.Numeric10, new Dictionary<int, SKSvg>() }
		};

		private readonly Dictionary<RasterCacheKey, SKSurface> _rasterCache = new Dictionary<RasterCacheKey, SKSurface>();
		private readonly Dictionary<SegmentType, RasterizeDimensions> _rasterizedDim = new Dictionary<SegmentType, RasterizeDimensions>();

		public static AlphaNumericResources GetInstance()
		{
			return _instance ?? (_instance = new AlphaNumericResources());
		}

		public SKSurface GetRasterized(int display, RasterizeLayer layer, SegmentType type, int segment)
		{
			var displayKey = new RasterCacheKey(display, layer, type, segment);
			if (_rasterCache.ContainsKey(displayKey)) {
				return _rasterCache[displayKey];
			}
			var initialKey = new RasterCacheKey(InitialCache, layer, type, segment);
			if (_rasterCache.ContainsKey(initialKey)) {
				return _rasterCache[initialKey];
			}
			return null;
		}

		public SKRect GetSvgSize(SegmentType type)
		{
			return _svgs[type][FullSegment].Picture.CullRect;
		}

		private AlphaNumericResources()
		{
			LoadAlphaNumeric();
			LoadNumeric8();
			LoadNumeric10();
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
			Load(segmentFileNames, $"{prefix}full.svg", SegmentType.Alphanumeric);
		}

		private void LoadNumeric8()
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
			Logger.Info("Loading numeric (8) SVGs...");
			Load(segmentFileNames, $"{prefix}full.svg", SegmentType.Numeric8);
		}

		private void LoadNumeric10()
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
				$"{prefix}08-center-top.svg",
				$"{prefix}09-center-bottom.svg",
			};
			Logger.Info("Loading numeric (10) SVGs...");
			Load(segmentFileNames, $"{prefix}full-10.svg", SegmentType.Numeric10);
		}

		private void Load(string[] fileNames, string pathToFull, SegmentType type)
		{
			for (var i = 0; i < fileNames.Length; i++) {
				var svg = new SKSvg();
				svg.Load(_assembly.GetManifestResourceStream(fileNames[i]));
				_svgs[type].Add(i, svg);
			}
			var full = new SKSvg();
			full.Load(_assembly.GetManifestResourceStream(pathToFull));
			_svgs[type].Add(FullSegment, full);
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

		public void Rasterize(int display, SegmentType type, RasterizeDimensions dim, RasterizeStyle style)
		{
			if (_rasterizedDim.ContainsKey(type) && _rasterizedDim[type].Equals(dim)) {
				Logger.Info("Already rasterized {0}, aborting.", type);
				return;
			}

			var scaledStyle = style.Scale(dim);
			var source = _svgs[type];

			Logger.Info("Rasterizing {0} segments for display {1} with scale = {2}, segment size = {3}x{4}", type, display, dim.SvgScale, dim.SvgWidth, dim.SvgHeight);
			using (var outerGlowPaint = new SKPaint()) {
				using (var innerGlowPaint = new SKPaint()) {
					using (var foregroundPaint = new SKPaint()) {

						outerGlowPaint.ColorFilter = SKColorFilter.CreateBlendMode(style.OuterGlow.Color, SKBlendMode.SrcIn);
						outerGlowPaint.ImageFilter = SKImageFilter.CreateBlur(scaledStyle.OuterGlow.Blur.X, scaledStyle.OuterGlow.Blur.Y,
							SKImageFilter.CreateDilate((int)scaledStyle.OuterGlow.Dilate.X, (int)scaledStyle.OuterGlow.Dilate.Y));

						innerGlowPaint.ColorFilter = SKColorFilter.CreateBlendMode(style.InnerGlow.Color, SKBlendMode.SrcIn);
						innerGlowPaint.ImageFilter = SKImageFilter.CreateBlur(scaledStyle.InnerGlow.Blur.X, scaledStyle.InnerGlow.Blur.Y,
							SKImageFilter.CreateDilate((int)scaledStyle.InnerGlow.Dilate.X, (int)scaledStyle.InnerGlow.Dilate.Y));

						foregroundPaint.ColorFilter = SKColorFilter.CreateBlendMode(style.Foreground.Color, SKBlendMode.SrcIn);
						foregroundPaint.ImageFilter = SKImageFilter.CreateBlur(scaledStyle.Foreground.Blur.X, scaledStyle.Foreground.Blur.Y);

						var layers = new List<Tuple<RasterizeLayer, SKPaint>> {
							new Tuple<RasterizeLayer, SKPaint>(RasterizeLayer.OuterGlow, outerGlowPaint),
							new Tuple<RasterizeLayer, SKPaint>(RasterizeLayer.InnerGlow, innerGlowPaint),
							new Tuple<RasterizeLayer, SKPaint>(RasterizeLayer.Foreground, foregroundPaint),
						};

						// for each layer...
						foreach (var i in source.Keys.Where(i => i != FullSegment)) {
							layers.ForEach(layer => {
								var initialKey = new RasterCacheKey(InitialCache, layer.Item1, type, i);
								var cacheKey = new RasterCacheKey(display, layer.Item1, type, i);
								if (!_rasterCache.ContainsKey(initialKey)) {
									_rasterCache[initialKey] = RasterizeSegment(source[i].Picture, dim, style, layer.Item2);
								} else {
									if (_rasterCache.ContainsKey(cacheKey)) {
										_rasterCache[cacheKey]?.Dispose();
									}
									_rasterCache[cacheKey] = RasterizeSegment(source[i].Picture, dim, style, layer.Item2);
								}
							});
						}
					}
				}
			}

			// unlit tubes
			using (var segmentUnlitPaint = new SKPaint()) {
				segmentUnlitPaint.ColorFilter = SKColorFilter.CreateBlendMode(style.Background.Color, SKBlendMode.SrcIn);
				segmentUnlitPaint.ImageFilter = SKImageFilter.CreateBlur(scaledStyle.Background.Blur.X, scaledStyle.Background.Blur.Y);

				var initialKey = new RasterCacheKey(InitialCache, RasterizeLayer.Background, type, FullSegment);
				var cacheKey = new RasterCacheKey(display, RasterizeLayer.Background, type, FullSegment);
				if (!_rasterCache.ContainsKey(initialKey)) {
					_rasterCache[initialKey] = RasterizeSegment(source[FullSegment].Picture, dim, style, segmentUnlitPaint);
				} else {
					if (_rasterCache.ContainsKey(cacheKey)) {
						_rasterCache[cacheKey]?.Dispose();
					}
					_rasterCache[cacheKey] = RasterizeSegment(source[FullSegment].Picture, dim, style, segmentUnlitPaint);
				}
			}

			_rasterizedDim[type] = dim;
			Logger.Info("Rasterization done.");
		}

		public void Clear()
		{
			_rasterCache.Clear();
			_rasterizedDim.Clear();
		}
	}

	public enum SegmentType
	{
		Alphanumeric, Numeric8, Numeric10
	}

	enum RasterizeLayer
	{
		OuterGlow, InnerGlow, Foreground, Background
	}

	public class RasterizeDimensions : IEquatable<RasterizeDimensions>
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
		public float OuterPaddingPercentage { get; set; } = 0.2f;
		public float SegmentPaddingPercentage { get; set; } = 0.3f;

		private readonly float _svgSkewedWidth;

		public RasterizeDimensions(SKRect svgSize, int width, int height, int numChars, int numLines, RasterizeStyle style)
		{
			NumChars = numChars;
			NumLines = numLines;

			OuterPadding = (int)Math.Round(OuterPaddingPercentage * height);
			SvgHeight = height - 2 * OuterPadding;
			SvgScale = SvgHeight / svgSize.Height;
			SvgWidth = svgSize.Width * SvgScale;
			LinePadding = (int)Math.Round(SvgHeight * LinePaddingPercentage);
			SvgMatrix = SKMatrix.MakeScale(SvgScale, SvgScale);
			_svgSkewedWidth = SkewedWidth(SvgWidth, SvgHeight, style.SkewAngle);
			SegmentPadding = (int)Math.Round(Math.Sqrt(SvgWidth * SvgWidth + SvgHeight * SvgHeight) * SegmentPaddingPercentage);
			SvgInfo = new SKImageInfo((int)(_svgSkewedWidth + 2 * SegmentPadding), (int)(SvgHeight + 2 * SegmentPadding));
			var skewedWidth = SkewedWidth(SvgWidth, SvgHeight, style.SkewAngle);
			CanvasWidth = (int)Math.Round(2 * OuterPadding + (NumChars - 1) * SvgWidth + skewedWidth);
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
			return SvgHeight.Equals(other.SvgHeight);
		}
	}

	public class RasterizeStyle
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

	public class RasterizeLayerStyle
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

	struct RasterCacheKey
	{
		public readonly int Display;
		public readonly RasterizeLayer Layer;
		public readonly SegmentType Type;
		public readonly int Segment;

		public RasterCacheKey(int display, RasterizeLayer layer, SegmentType type, int segment)
		{
			Display = display;
			Layer = layer;
			Type = type;
			Segment = segment;
		}
	}

}
