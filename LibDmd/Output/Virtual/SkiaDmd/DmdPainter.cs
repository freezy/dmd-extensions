using System;
using System.Collections.Generic;
using System.Linq;
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

		public static void Paint(DmdData data, SKCanvas canvas, int width, int height, DmdStyleDefinition style, bool cache)
		{
			canvas.Clear(style.BackgroundColor);
			if (style.OuterGlow.IsEnabled) {
				if (cache) {
					PaintCached(data, canvas, width, height, style.OuterGlow, DmdLayer.OuterGlow);
				} else {
					PaintDirectly(data, canvas, width, height, style.OuterGlow);
				}
			}
			if (style.InnerGlow.IsEnabled) {
				if (cache) {
					PaintCached(data, canvas, width, height, style.InnerGlow, DmdLayer.InnerGlow);
				} else {
					PaintDirectly(data, canvas, width, height, style.InnerGlow);
				}

			}
			if (style.Foreground.IsEnabled) {
				if (cache) {
					PaintCached(data, canvas, width, height, style.Foreground, DmdLayer.Foreground);
				} else {
					PaintDirectly(data, canvas, width, height, style.Foreground);
				}
			}
		}

		private static void PaintCached(DmdData data, SKCanvas canvas, int width, int height, DmdLayerStyleDefinition styleDef, DmdLayer layer)
		{
			var cacheInfo = CacheInfo.ContainsKey(layer) ? CacheInfo[layer] : null;
			var cachedSurface = Cache.ContainsKey(layer) ? Cache[layer] : null;
			var currentCacheInfo = new CacheInfo(width, height, styleDef);

			var blockSize = GetBlockSize(data, width, height);
			var dotSize = GetDotSize(styleDef, blockSize);
			var surfaceSize = GetCacheSurfaceSize(styleDef, blockSize);
			
			if (cachedSurface == null || cacheInfo == null || !currentCacheInfo.Equals(cacheInfo)) {
				// create cache
				cachedSurface?.Dispose();
				Logger.Info("Painting new cache for layer {0} at {1}x{2}", layer, surfaceSize.Width, surfaceSize.Height);

				cachedSurface = PaintCache(layer, styleDef, blockSize, dotSize, surfaceSize);
				CacheInfo[layer] = currentCacheInfo;
				Cache[layer] = cachedSurface;
			}

			for (var y = 0; y < data.Height; y++) {
				for (var x = 0; x < data.Width * 3; x += 3) {
					var framePos = y * data.Width * 3 + x;
					var dotPos = new SKPoint(x / 3f * blockSize.Width, y * blockSize.Height);

					// don't render black dots at all
					if (data.Data[framePos] == 0 && data.Data[framePos + 1] == 0 && data.Data[framePos + 2] == 0) {
						continue;
					}

					var color = new SKColor(data.Data[framePos], data.Data[framePos + 1], data.Data[framePos + 2]);
					using (var dotPaint = new SKPaint()) {
						dotPaint.ColorFilter = SKColorFilter.CreateBlendMode(GetColor(color, styleDef), SKBlendMode.SrcIn);
						canvas.DrawSurface(cachedSurface, dotPos.X + (blockSize.Width - surfaceSize.Width) / 2, dotPos.Y + (blockSize.Height - surfaceSize.Height) / 2, dotPaint);
					}
				}
			}
		}

		private static SKSurface PaintCache(DmdLayer layer, DmdLayerStyleDefinition styleDef, SKSize blockSize, SKSize dotSize, SKSize surfaceSize)
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

		private static void PaintDirectly(DmdData data, SKCanvas canvas, int width, int height, DmdLayerStyleDefinition styleDef)
		{
			var size = GetBlockSize(data, width, height);
			var dotSize = new SKSize((float)styleDef.Size * width / data.Width, (float)styleDef.Size * height / data.Height);
			for (var y = 0; y < data.Height; y++) {
				for (var x = 0; x < data.Width * 3; x += 3) {
					var framePos = y * data.Width * 3 + x;

					// don't render black dots at all
					if (data.Data[framePos] == 0 && data.Data[framePos + 1] == 0 && data.Data[framePos + 2] == 0) {
						continue;
					}

					var color = new SKColor(data.Data[framePos], data.Data[framePos + 1], data.Data[framePos + 2]);
					using (var dotPaint = new SKPaint()) {
						dotPaint.IsAntialias = true;
						dotPaint.Color = GetColor(color, styleDef);

						if (styleDef.IsBlurEnabled) {
							var blur = GetBlur(styleDef, size);
							dotPaint.ImageFilter = SKImageFilter.CreateBlur(blur, blur);
						}
						var dotPos = new SKPoint(x / 3f * size.Width, y * size.Height);
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
				}
			}
		}

		private static SKSize GetBlockSize(DmdData data, int width, int height)
		{
			return new SKSize((float)width / data.Width, (float)height / data.Height);
		}

		private static SKSize GetDotSize(DmdLayerStyleDefinition styleDef, SKSize blockSize)
		{
			return new SKSize((float)styleDef.Size * blockSize.Width, (float)styleDef.Size * blockSize.Height);
		}

		private static SKSize GetCacheSurfaceSize(DmdLayerStyleDefinition styleDef, SKSize blockSize)
		{
			var blur = GetBlur(styleDef, blockSize);
			return new SKSize(blockSize.Width + 10 * blur, blockSize.Height + 10 * blur);
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
			if (Math.Abs(styleDef.Luminosity) > 0.01) {
				color.ToHsl(out var h, out var s, out var l);
				color = SKColor.FromHsl(h, s, Math.Max(0, Math.Min(100, l + styleDef.Luminosity)));
			}
			if (styleDef.Opacity < 1) {
				color = color.WithAlpha((byte)(256 * styleDef.Opacity));
			}
			return color;
		}
	}

	public class DmdData
	{
		public readonly byte[] Data;
		public readonly int Width;
		public readonly int Height;

		public DmdData(byte[] data, int width, int height)
		{
			Data = data;
			Width = width;
			Height = height;
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

		public int GetHashCode(CacheInfo obj)
		{
			unchecked {
				var hashCode = obj.Width;
				hashCode = (hashCode * 397) ^ obj.Height;
				hashCode = (hashCode * 397) ^ (obj.StyleDef != null ? obj.StyleDef.GetHashCode() : 0);
				return hashCode;
			}
		}
	}
}
