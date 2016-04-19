using System;

namespace LibDmd.Common
{
	public class ColorUtil
	{
		/// <summary>
		/// Converts an RGB color to HSL
		/// </summary>
		/// <param name="red">Red input, 0-255</param>
		/// <param name="green">Green input, 0-255</param>
		/// <param name="blue">Blue input, 0-255</param>
		/// <param name="hue">Hue output, 0.0 - 6.0</param>
		/// <param name="saturation">Saturation output, 0.0 - 1.0</param>
		/// <param name="luminosity">Luminosity output, 0.0 - 1.0</param>
		public static void RgbToHsl(byte red, byte green, byte blue, out double hue, out double saturation, out double luminosity)
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
		public static void HslToRgb(double hue, double saturation, double luminosity, out byte red, out byte green, out byte blue)
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
