using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Windows.Forms;
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

		private SKSurface RasterizeSegment(SKPicture segment, RasterizeDimensions dim, float skewAngle, params SKPaint[] paints)
		{
			if (dim.SvgWidth <= 0 || dim.SvgWidth <= 0) {
				return null;
			}
			var surface = SKSurface.Create(dim.SvgInfo);
			surface.Canvas.Translate(dim.Translate.X, dim.Translate.Y);
			Skew(surface.Canvas, skewAngle, 0);
			foreach (var paint in paints) {
				surface.Canvas.DrawPicture(segment, ref dim.SvgMatrix, paint);
			}
			return surface;
		}

		private static void Skew(SKCanvas canvas, double xDegrees, double yDegrees)
		{
			canvas.Skew((float)Math.Tan(Math.PI * xDegrees / 180), (float)Math.Tan(Math.PI * yDegrees / 180));
		}

		public void Rasterize(DisplaySetting setting, bool force = false)
		{
			if (!force && _rasterizedDim.ContainsKey(setting.SegmentType) && _rasterizedDim[setting.SegmentType].Equals(setting.Dim)) {
				Logger.Info("Already rasterized {0}, aborting.", setting.SegmentType);
				return;
			}

			var source = _svgs[setting.SegmentType];

			Rasterize(setting, RasterizeLayer.OuterGlow, setting.Style.OuterGlow, setting.Style.SkewAngle);
			Rasterize(setting, RasterizeLayer.InnerGlow, setting.Style.InnerGlow, setting.Style.SkewAngle);
			Rasterize(setting, RasterizeLayer.Foreground, setting.Style.Foreground, setting.Style.SkewAngle);

			// unlit tubes
			using (var segmentUnlitPaint = new SKPaint()) {
				segmentUnlitPaint.ColorFilter = SKColorFilter.CreateBlendMode(setting.Style.Background.Color, SKBlendMode.SrcIn);
				segmentUnlitPaint.ImageFilter = SKImageFilter.CreateBlur(setting.Style.Background.Blur.X, setting.Style.Background.Blur.Y);

				var initialKey = new RasterCacheKey(InitialCache, RasterizeLayer.Background, setting.SegmentType, FullSegment);
				var cacheKey = new RasterCacheKey(setting.Display, RasterizeLayer.Background, setting.SegmentType, FullSegment);
				if (!_rasterCache.ContainsKey(initialKey)) {
					_rasterCache[initialKey] = RasterizeSegment(source[FullSegment].Picture, setting.Dim, setting.Style.SkewAngle, segmentUnlitPaint);
				} else {
					if (_rasterCache.ContainsKey(cacheKey)) {
						_rasterCache[cacheKey]?.Dispose();
					}
					_rasterCache[cacheKey] = RasterizeSegment(source[FullSegment].Picture, setting.Dim, setting.Style.SkewAngle, segmentUnlitPaint);
				}
			}

			_rasterizedDim[setting.SegmentType] = setting.Dim;
			Logger.Info("Rasterization done.");
		}

		public void Rasterize(DisplaySetting setting, RasterizeLayer layer, RasterizeLayerStyle layerStyle, float skewAngle)
		{
			var source = _svgs[setting.SegmentType];
			Logger.Info("Rasterizing {0} segments of layer {1} on display {2} segment size = {3}x{4}", setting.SegmentType, layer, setting.Display, setting.Dim.SvgWidth, setting.Dim.SvgHeight);
			using (var paint = new SKPaint()) {
				ApplyFilters(paint, layerStyle);
				foreach (var i in source.Keys.Where(i => i != FullSegment)) {
					var initialKey = new RasterCacheKey(InitialCache, layer, setting.SegmentType, i);
					var cacheKey = new RasterCacheKey(setting.Display, layer, setting.SegmentType, i);
					if (!_rasterCache.ContainsKey(initialKey)) {
						_rasterCache[initialKey] = RasterizeSegment(source[i].Picture, setting.Dim, skewAngle, paint);
					} else {
						if (_rasterCache.ContainsKey(cacheKey)) {
							_rasterCache[cacheKey]?.Dispose();
						}
						_rasterCache[cacheKey] = RasterizeSegment(source[i].Picture, setting.Dim, skewAngle, paint);
					}
				}
			}
		}

		public void Clear()
		{
			_rasterCache.Clear();
			_rasterizedDim.Clear();
		}

		private void ApplyFilters(SKPaint paint, RasterizeLayerStyle layerStyle)
		{
			paint.ColorFilter = SKColorFilter.CreateBlendMode(layerStyle.Color, SKBlendMode.SrcIn);
			if (layerStyle.IsBlurEnabled && layerStyle.IsDilateEnabled) {
				paint.ImageFilter = SKImageFilter.CreateBlur(layerStyle.Blur.X, layerStyle.Blur.Y,
					SKImageFilter.CreateDilate((int)layerStyle.Dilate.X, (int)layerStyle.Dilate.Y));
			} else if (layerStyle.IsBlurEnabled) {
				paint.ImageFilter = SKImageFilter.CreateBlur(layerStyle.Blur.X, layerStyle.Blur.Y);
			} else if (layerStyle.IsDilateEnabled) {
				paint.ImageFilter = SKImageFilter.CreateDilate((int)layerStyle.Dilate.X, (int)layerStyle.Dilate.Y);
			}

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
