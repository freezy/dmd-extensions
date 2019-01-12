using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LibDmd.Common;
using NLog;
using SkiaSharp;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	public class DmdPainter
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly Dictionary<DmdLayer, SKSurface> Cache = new Dictionary<DmdLayer, SKSurface>();
		private static readonly Dictionary<DmdLayer, CacheInfo> CacheInfo = new Dictionary<DmdLayer, CacheInfo>();

		public static void Paint(DmdFrame frame, SKCanvas canvas, int width, int height, DmdStyleDefinition style, bool cache)
		{
			canvas.Clear(style.BackgroundColor);
			if (style.Background.IsEnabled) {
				if (cache) {
					PaintCached(frame, canvas, width, height, style.Background, DmdLayer.Background);
				} else {
					PaintDirectly(frame, canvas, width, height, style.Background);
				}
			}
			if (style.OuterGlow.IsEnabled) {
				if (cache) {
					PaintCached(frame, canvas, width, height, style.OuterGlow, DmdLayer.OuterGlow);
				} else {
					PaintDirectly(frame, canvas, width, height, style.OuterGlow);
				}
			}
			if (style.InnerGlow.IsEnabled) {
				if (cache) {
					PaintCached(frame, canvas, width, height, style.InnerGlow, DmdLayer.InnerGlow);
				} else {
					PaintDirectly(frame, canvas, width, height, style.InnerGlow);
				}

			}
			if (style.Foreground.IsEnabled) {
				if (cache) {
					PaintCached(frame, canvas, width, height, style.Foreground, DmdLayer.Foreground);
				} else {
					PaintDirectly(frame, canvas, width, height, style.Foreground);
				}
			}
		}

		private static void PaintCached(DmdFrame frame, SKCanvas canvas, int width, int height, DmdLayerStyleDefinition styleDef, DmdLayer layer)
		{
			var cacheInfo = CacheInfo.ContainsKey(layer) ? CacheInfo[layer] : null;
			var cachedSurface = Cache.ContainsKey(layer) ? Cache[layer] : null;
			var currentCacheInfo = new CacheInfo(width, height, styleDef);

			var blockSize = GetBlockSize(frame, width, height);
			var dotSize = GetDotSize(styleDef, blockSize);
			var surfaceSize = GetCacheSurfaceSize(styleDef, blockSize);
			
			if (cachedSurface == null || cacheInfo == null || !currentCacheInfo.Equals(cacheInfo)) {
				// create cache
				cachedSurface?.Dispose();
				Logger.Info("Painting new cache for layer {0} at {1}x{2}", layer, surfaceSize.Width, surfaceSize.Height);

				cachedSurface = PaintCache(styleDef, blockSize, dotSize, surfaceSize);
				CacheInfo[layer] = currentCacheInfo;
				Cache[layer] = cachedSurface;
			}

			var painter = GetPainter(frame.Format);
			painter?.Invoke(frame, styleDef, (x, y, color) => {
				var dotPos = new SKPoint(x * blockSize.Width, y * blockSize.Height);
				using (var dotPaint = new SKPaint()) {
					dotPaint.ColorFilter = SKColorFilter.CreateBlendMode(GetColor(color, styleDef), SKBlendMode.SrcIn);
					canvas.DrawSurface(cachedSurface, dotPos.X + (blockSize.Width - surfaceSize.Width) / 2, dotPos.Y + (blockSize.Height - surfaceSize.Height) / 2, dotPaint);
				}
			});
		}

		private static SKSurface PaintCache(DmdLayerStyleDefinition styleDef, SKSize blockSize, SKSize dotSize, SKSize surfaceSize)
		{
			var surface = GLUtil.GetInstance().CreateSurface((int)surfaceSize.Width, (int)surfaceSize.Height);
			using (var dotPaint = new SKPaint()) {
				dotPaint.IsAntialias = true;
				dotPaint.Color = SKColors.Black;

				if (styleDef.IsBlurEnabled) {
					var blur = GetBlur(styleDef, blockSize);
					dotPaint.ImageFilter = SKImageFilter.CreateBlur(blur, blur);
				}
				if (styleDef.IsRoundedEnabled) {
					if (styleDef.Rounded < 1) {
						var cornerRadius = Math.Min(dotSize.Width, dotSize.Height) * (float)styleDef.Rounded / 2;
						surface.Canvas.DrawRoundRect(surfaceSize.Width / 2 - dotSize.Width / 2, surfaceSize.Height / 2 - dotSize.Width / 2, dotSize.Width, dotSize.Height, cornerRadius, cornerRadius, dotPaint);
					} else {
						var dotRadius = Math.Min(dotSize.Width, dotSize.Height) / 2;
						surface.Canvas.DrawCircle(surfaceSize.Width / 2, surfaceSize.Height / 2, dotRadius, dotPaint);
					}
				} else {
					surface.Canvas.DrawRect(surfaceSize.Width / 2 - dotSize.Width / 2, surfaceSize.Height / 2 - dotSize.Width / 2, dotSize.Width, dotSize.Height, dotPaint);
				}
			}
			return surface;
		}

		private static void PaintDirectly(DmdFrame frame, SKCanvas canvas, int width, int height, DmdLayerStyleDefinition styleDef)
		{
			var size = GetBlockSize(frame, width, height);
			var dotSize = new SKSize((float)styleDef.Size * width / frame.Width, (float)styleDef.Size * height / frame.Height);
			var painter = GetPainter(frame.Format);

			painter?.Invoke(frame, styleDef, (x, y, color) => {
				using (var dotPaint = new SKPaint()) {
					dotPaint.IsAntialias = true;
					dotPaint.Color = GetColor(color, styleDef);

					if (styleDef.IsBlurEnabled) {
						var blur = GetBlur(styleDef, size);
						dotPaint.ImageFilter = SKImageFilter.CreateBlur(blur, blur);
					}
					var dotPos = new SKPoint(x * size.Width, y * size.Height);
					if (styleDef.IsRoundedEnabled) {
						if (styleDef.Rounded < 1) {
							var cornerRadius = Math.Min(dotSize.Width, dotSize.Height) * (float)styleDef.Rounded / 2;
							canvas.DrawRoundRect(dotPos.X + size.Width / 2 - dotSize.Width / 2, dotPos.Y + size.Height / 2 - dotSize.Width / 2, dotSize.Width, dotSize.Height, cornerRadius, cornerRadius, dotPaint);
						} else {
							var dotRadius = Math.Min(dotSize.Width, dotSize.Height) / 2;
							canvas.DrawCircle(dotPos.X + size.Width / 2, dotPos.Y + size.Height / 2, dotRadius, dotPaint);
						}
					} else {
						canvas.DrawRect(dotPos.X + size.Width / 2 - dotSize.Width / 2, dotPos.Y + size.Height / 2 - dotSize.Width / 2, dotSize.Width, dotSize.Height, dotPaint);
					}
				}
			});
		}

		private static Action<DmdFrame, DmdLayerStyleDefinition, Action<int, int, SKColor>> GetPainter(FrameFormat format)
		{
			switch (format) {
				case FrameFormat.Gray2:
					return PaintGray2;
				case FrameFormat.Gray4:
					return PaintGray4;
				case FrameFormat.Rgb24:
					return PaintRgb24;
			}
			return null;
		}

		private static void PaintGray2(DmdFrame frame, DmdLayerStyleDefinition styleDef, Action<int, int, SKColor> paint)
		{
			for (var y = 0; y < frame.Height; y++) {
				for (var x = 0; x < frame.Width; x++) {
					var framePos = y * frame.Width + x;
					if (styleDef.IsUnlit || frame.Data[framePos] != 0) {
						if (frame.Palette != null && frame.Palette.Length < frame.Data[framePos]) {
							paint(x, y, GetTransparentColor(frame.Palette[frame.Data[framePos]]));
						} else {
							paint(x, y, frame.Color.WithAlpha((byte)(frame.Data[framePos] * 85)));
						}
					}
				}
			}
		}

		private static void PaintGray4(DmdFrame frame, DmdLayerStyleDefinition styleDef, Action<int, int, SKColor> paint)
		{
			for (var y = 0; y < frame.Height; y++) {
				for (var x = 0; x < frame.Width; x++) {
					var framePos = y * frame.Width + x;
					if (styleDef.IsUnlit || frame.Data[framePos] != 0) {
						if (frame.Palette != null && frame.Palette.Length < frame.Data[framePos]) {
							paint(x, y, GetTransparentColor(frame.Palette[frame.Data[framePos]]));
						} else {
							paint(x, y, frame.Color.WithAlpha((byte)(frame.Data[framePos] * 17)));
						}
					}
				}
			}
		}

		private static void PaintRgb24(DmdFrame frame, DmdLayerStyleDefinition styleDef, Action<int, int, SKColor> paint)
		{
			for (var y = 0; y < frame.Height; y++) {
				for (var x = 0; x < frame.Width * 3; x += 3) {
					var framePos = y * frame.Width * 3 + x;
					if (styleDef.IsUnlit || frame.Data[framePos] != 0 || frame.Data[framePos + 1] != 0 || frame.Data[framePos + 2] != 0) {
						paint((int)(x / 3f), y, GetTransparentColor(new SKColor(frame.Data[framePos], frame.Data[framePos + 1], frame.Data[framePos + 2])));
					}
				}
			}
		}

		private static SKSize GetBlockSize(DmdFrame frame, int width, int height)
		{
			return new SKSize((float)width / frame.Width, (float)height / frame.Height);
		}

		private static SKSize GetDotSize(DmdLayerStyleDefinition styleDef, SKSize blockSize)
		{
			return new SKSize((float)styleDef.Size * blockSize.Width, (float)styleDef.Size * blockSize.Height);
		}

		private static SKSize GetCacheSurfaceSize(DmdLayerStyleDefinition styleDef, SKSize blockSize)
		{
			var blur = GetBlur(styleDef, blockSize);
			return new SKSize(blockSize.Width * (float)styleDef.Size + 10 * blur + 5, blockSize.Height * (float)styleDef.Size + 10 * blur + 5);
		}

		private static float GetBlur(DmdLayerStyleDefinition styleDef, SKSize blockSize)
		{
			if (styleDef.IsBlurEnabled) {
				return (float)styleDef.Blur / Math.Max(blockSize.Width, blockSize.Height);
			}
			return 0f;
		}

		private static SKColor GetColor(SKColor color, DmdLayerStyleDefinition styleDef)
		{
			if (styleDef.IsUnlit) {
				return styleDef.UnlitColor.WithAlpha((byte)(styleDef.UnlitColor.Alpha * styleDef.Opacity)); ;
			}
			if (Math.Abs(styleDef.Luminosity) > 0.01 || Math.Abs(styleDef.Hue) != 0.0) {
				color.ToHsv(out var h, out var s, out var l);
				color = SKColor.FromHsv((h + styleDef.Hue % 360), s, Math.Max(0, Math.Min(100, l + styleDef.Luminosity))).WithAlpha(color.Alpha);
			}
			if (styleDef.Opacity < 1) {
				color = color.WithAlpha((byte)(color.Alpha * styleDef.Opacity));
			}
			return color;
		}

		private static SKColor GetTransparentColor(SKColor color)
		{
			color.ToHsv(out var h, out var s, out var v);
			return SKColor.FromHsv(h, s, 100, (byte)(2.55 * v));
		}
	}

	public class DmdFrame
	{
		public readonly FrameFormat Format;
		public readonly byte[] Data;
		public readonly int Width;
		public readonly int Height;
		public readonly SKColor Color;
		public readonly SKColor[] Palette;

		public DmdFrame(FrameFormat format, byte[] data, int width, int height)
		{
			Format = format;
			Data = data;
			Width = width;
			Height = height;
			Color = new SKColor(RenderGraph.DefaultColor.R, RenderGraph.DefaultColor.G, RenderGraph.DefaultColor.B, RenderGraph.DefaultColor.A); ;
			Palette = null;
		}

		public DmdFrame(FrameFormat format, byte[] data, int width, int height, SKColor color, SKColor[] palette)
		{
			Format = format;
			Data = data;
			Width = width;
			Height = height;
			Color = color;
			Palette = palette;
		}
	}

	internal class CacheInfo
	{
		public readonly int Width;
		public readonly int Height;
		public readonly DmdLayerStyleDefinition StyleDef;

		public CacheInfo(int width, int height, DmdLayerStyleDefinition styleDef)
		{
			Width = width;
			Height = height;
			StyleDef = styleDef;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is CacheInfo item)) {
				return false;
			}
			return Width == item.Width
			       && Height == item.Height
			       && StyleDef.Equals(item.StyleDef);
		}

		protected bool Equals(CacheInfo other)
		{
			return Width == other.Width
			       && Height == other.Height
			       && StyleDef.Equals(other.StyleDef);
		}

		public override int GetHashCode()
		{
			var hashCode = 441766974;
			hashCode = hashCode * -1521134295 + Width.GetHashCode();
			hashCode = hashCode * -1521134295 + Height.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<DmdLayerStyleDefinition>.Default.GetHashCode(StyleDef);
			return hashCode;
		}
	}
}
