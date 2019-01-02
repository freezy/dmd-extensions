using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	public class DmdPainter
	{
		public static void Paint(DmdData data, SKCanvas canvas, int width, int height, DmdStyleDefinition style)
		{
			canvas.Clear(style.BackgroundColor);
			if (style.OuterGlow.IsEnabled) {
				PaintLayer(data, canvas, width, height, style.OuterGlow);
			}
			if (style.InnerGlow.IsEnabled) {
				PaintLayer(data, canvas, width, height, style.InnerGlow);
			}
			if (style.Foreground.IsEnabled) {
				PaintLayer(data, canvas, width, height, style.Foreground);
			}
		}

		private static void PaintLayer(DmdData data, SKCanvas canvas, int width, int height, DmdLayerStyleDefinition styleDef)
		{
			var size = new SKSize((float)width / data.Width, (float)height / data.Height);
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
						dotPaint.Color = color;
						if (Math.Abs(styleDef.Luminosity) > 0.01) {
							dotPaint.Color.ToHsl(out var h, out var s, out var l);
							dotPaint.Color = SKColor.FromHsl(h, s, Math.Max(0, Math.Min(100, l + styleDef.Luminosity)));
						}
						if (styleDef.Opacity < 1) {
							dotPaint.Color = dotPaint.Color.WithAlpha((byte)(256 * styleDef.Opacity));
						}
						if (styleDef.IsBlurEnabled) {
							var blur = (float)styleDef.Blur / Math.Max(size.Width, size.Height);
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
}
