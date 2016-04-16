using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PinDmd.Processor
{
	/// <summary>
	/// Changes the luminosity of every pixel to a restricted number of shades.
	/// </summary>
	public class ShadeProcessor : AbstractProcessor
	{
		/// <summary>
		/// Number of shades to reduce luminosity to. Shades are equally 
		/// distributed but can be tweaked with <see cref="Intensity"/> and
		/// <see cref="Lightness"/>.
		/// </summary>
		/// <remarks>Set to 0 to disable shading (only intensity and lightness apply).</remarks>
		public int NumShades { get; set; } = 4;

		/// <summary>
		/// Multiplies the intensity with the given value. This is useful for
		/// reaching the full dynamic before cutting them down to the reduced
		/// number of shades.
		/// </summary>
		public double Intensity { get; set; } = 1;

		/// <summary>
		/// Adds additional intensity. Values between 0 and 1, where 1 will 
		/// transform all pixels to full white. Useful if <see cref="Intensity"/>
		/// can only be increased so much before overflowing, and full intensity
		/// still needs to be achieved.
		/// </summary>
		public double Lightness { get; set; } = 0;

		public override BitmapSource Process(BitmapSource bmp)
		{
			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var stride = bmp.PixelWidth * bytesPerPixel;
			var pixelBuffer = new byte[stride * bmp.PixelHeight];
			var fullRect = new Int32Rect { X = 0, Y = 0, Width = bmp.PixelWidth, Height = bmp.PixelHeight };

			bmp.CopyPixels(fullRect, pixelBuffer, stride, 0);

			for (var k = 0; k + 4 < pixelBuffer.Length; k += 4)
			{
				// convert to HSL
				var b = pixelBuffer[k];
				var g = pixelBuffer[k + 1];
				var r = pixelBuffer[k + 2];

				double hue;
				double saturation;
				double luminosity;
				ColorToHSL(r, g, b, out hue, out saturation, out luminosity);

				// manipulate luminosity
				if (NumShades > 0) {
					luminosity = (Math.Round(luminosity * Intensity * NumShades) + Lightness) / NumShades;

				} else {
					luminosity = luminosity * Intensity + Lightness;
				}

				// convert back to RGB
				byte red;
				byte green;
				byte blue;
				ColorFromHSL(hue, saturation, luminosity, out red, out green, out blue);

				pixelBuffer[k] = blue;
				pixelBuffer[k + 1] = green;
				pixelBuffer[k + 2] = red;
			}

			var dest = new WriteableBitmap(bmp);
			dest.WritePixels(fullRect, pixelBuffer, stride, 0);
			dest.Freeze();

			_whenProcessed.OnNext(dest);
			return dest;
		}

		/// <summary>
		/// Converts an RGB color to HSL
		/// </summary>
		/// <param name="red">Red input</param>
		/// <param name="green">Green input</param>
		/// <param name="blue">Blue input</param>
		/// <param name="hue">Hue output</param>
		/// <param name="saturation">Saturation output</param>
		/// <param name="luminosity">Luminosity output</param>
		private static void ColorToHSL(byte red, byte green, byte blue, out double hue, out double saturation, out double luminosity)
		{
			var r = (red / 255d);
			var g = (green / 255d);
			var b = (blue / 255d);

			var min = Math.Min(Math.Min(r, g), b);
			var max = Math.Max(Math.Max(r, g), b);
			var delta = max - min;

			var h = 0d;
			var s = 0d;
			var l = (max + min) / 2.0d;

			if (delta != 0d) {
				if (l < 0.5f) {
					s = delta / (max + min);
				} else {
					s = delta / (2.0d - max - min);
				}
				if (r == max) {
					h = (g - b) / delta;
				} else if (g == max) {
					h = 2d + (b - r) / delta;
				} else if (b == max) {
					h = 4d + (r - g) / delta;
				}
			}
			hue = h;
			saturation = s;
			luminosity = l;
		}

		/// <summary>
		/// Converts a HSL color to RGB
		/// </summary>
		/// <param name="hue">Hue input</param>
		/// <param name="saturation">Saturation input</param>
		/// <param name="luminosity">Luminosity input</param>
		/// <param name="red">Red output</param>
		/// <param name="green">Green output</param>
		/// <param name="blue">Blue output</param>
		public static void ColorFromHSL(double hue, double saturation, double luminosity, out byte red, out byte green, out byte blue)
		{
			byte r, g, b;
			if (saturation == 0) {
				r = (byte)Math.Round(luminosity * 255d);
				g = (byte)Math.Round(luminosity * 255d);
				b = (byte)Math.Round(luminosity * 255d);

			} else {
				double t2;
				var th = hue / 6.0d;

				if (luminosity < 0.5d) {
					t2 = luminosity * (1d + saturation);
				} else {
					t2 = (luminosity + saturation) - (luminosity * saturation);
				}
				var t1 = 2d * luminosity - t2;

				var tr = th + (1.0d / 3.0d);
				var tg = th;
				var tb = th - (1.0d / 3.0d);

				tr = ColorCalc(tr, t1, t2);
				tg = ColorCalc(tg, t1, t2);
				tb = ColorCalc(tb, t1, t2);

				r = (byte)Math.Round(tr * 255d);
				g = (byte)Math.Round(tg * 255d);
				b = (byte)Math.Round(tb * 255d);
			}

			red = r;
			green = g;
			blue = b;
		}

		private static double ColorCalc(double c, double t1, double t2)
		{
			if (c < 0) {
				c += 1d;
			}
			if (c > 1) {
				c -= 1d;
			}
			if (6.0d * c < 1.0d) {
				return t1 + (t2 - t1) * 6.0d * c;
			}
			if (2.0d * c < 1.0d) {
				return t2;
			}
			if (3.0d * c < 2.0d) {
				return t1 + (t2 - t1) * (2.0d / 3.0d - c) * 6.0d;
			}
			return t1;
		}
	}
}
