using System;
using System.Windows.Media;

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
			if (Math.Abs(saturation) < 0.001) {
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


		/// <summary>
		/// Checks if a given string can be interpreted as color
		/// </summary>
		/// <param name="c">String to parse as color</param>
		/// <returns>True if color, false otherwise</returns>
		public static bool IsColor(string c)
		{
			try {
				ColorConverter.ConvertFromString(c.StartsWith("#") ? c : "#FF" + c);
				return true;
			} catch (FormatException e) {
				Console.WriteLine(e);
				return false;
			}
		}

		/// <summary>
		/// Parses a string to a color
		/// </summary>
		/// <param name="c">String to parse as color</param>
		/// <returns>Parsed color</returns>
		public static Color ParseColor(string c)
		{
			return (Color)ColorConverter.ConvertFromString(c.StartsWith("#") ? c : "#FF" + c);
		}

		/// <summary>
		/// Sets the 16-color gray4 palette.
		/// 
		/// Any number of colors can be provided, they are interpolated if they
		/// don't match the bit length
		/// </summary>
		/// <param name="palette">Color to assign to each gray shade</param>
		/// <param name="numTones">Number of shades to return</param>
		/// <returns></returns>
		public static Color[] GetGrayPalette(Color[] palette, int numTones)
		{
			if (palette.Length == 0) {
				return null;
			}
			if (palette.Length == 1) {
				throw new ArgumentException("Must provide at least 2 colors.");
			}
			if (palette.Length == numTones) {
				return palette;
			}

			// else interpolate
			var interpolatedPalette = new Color[numTones];
			var toRasterSize = 1d / (numTones - 1);
			var fromRasterPos = 0;
			var fromRasterSize = 1d / (palette.Length - 1);
			for (var toRasterPos = 0; toRasterPos < numTones; toRasterPos++) {
				
				var fromColorPos = fromRasterPos * fromRasterSize;
				var toColorPos = toRasterPos * toRasterSize;
				var relativePos = (toColorPos - fromColorPos) / fromRasterSize;

				var fromColorStart = palette[fromRasterPos];
				var fromColorEnd = palette[fromRasterPos + 1];

				interpolatedPalette[toRasterPos] = MixColors(fromColorStart, fromColorEnd, relativePos);

				if (fromRasterSize * (fromRasterPos + 1) < toRasterSize * (toRasterPos + 1)) {
					fromRasterPos++;
				}
			}
			return interpolatedPalette;
		}

		/// <summary>
		/// Returns an RGB24 array with colors from the palette applied to the frame.
		/// 
		/// Note that the size of the palette must be as large as the largest integer of 
		/// the frame to color, or in other words, the bit length is given by the size of
		/// the palette and the values of the frame.
		/// </summary>
		/// <param name="width">Width of the frame to color</param>
		/// <param name="height">Height of the frame to color</param>
		/// <param name="frame">Frame to color, width * height pixels with values from 0 - [size of palette]</param>
		/// <param name="palette">Colors to use for coloring</param>
		/// <returns></returns>
		public static byte[] ColorizeFrame(int width, int height, byte[] frame, Color[] palette)
		{
			var colorizedFrame = new byte[width * height * 3];
			var pos = 0;
			for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x++) {
					var pixel = frame[y * width + x];
					colorizedFrame[pos] = palette[pixel].R;
					colorizedFrame[pos + 1] = palette[pixel].G;
					colorizedFrame[pos + 2] = palette[pixel].B;
					pos += 3;
				}
			}
			return colorizedFrame;
		}

		private static Color MixColors(Color color1, Color color2, double p)
		{
			return Color.FromRgb(
				(byte)Math.Round(color1.R * (1 - p) + color2.R * p),
				(byte)Math.Round(color1.G * (1 - p) + color2.G * p),
				(byte)Math.Round(color1.B * (1 - p) + color2.B * p)
			);
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
