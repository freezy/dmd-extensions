using System;
using System.Collections.Generic;
using System.Windows.Media;
using LibDmd.Frame;

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
		/// Sets the palette for a given bit length.
		/// 
		/// Any number of colors can be provided, they are interpolated if they
		/// don't match the bit length.
		/// </summary>
		/// <param name="palette">Color to assign to each gray shade</param>
		/// <param name="numColors">Number of shades to return</param>
		/// <returns></returns>
		public static Color[] GetPalette(Color[] palette, int numColors)
		{
			if (palette.Length == 0) {
				return null;
			}
			if (palette.Length == 1) {
				throw new ArgumentException("Must provide at least 2 colors.");
			}
			if (palette.Length == numColors) {
				return palette;
			}
			if ((palette.Length == 16 || palette.Length == 64) && numColors == 4) {
				return new[] { palette[0], palette[1], palette[4], palette[15] };
			}
			if (palette.Length == 4 && (palette.Length == 16 || palette.Length == 64)) {
				return new[] { palette[0], palette[1], Colors.Black, Colors.Black, palette[2], Colors.Black, Colors.Black, Colors.Black, Colors.Black, Colors.Black, Colors.Black, Colors.Black, Colors.Black, Colors.Black, Colors.Black, palette[3] };
			}
			if (palette.Length == 64 && numColors == 16)
			{
				return new[] { palette[0], palette[1], palette[2], palette[3], palette[4], palette[5], palette[6], palette[7], palette[8], palette[9], palette[10], palette[11], palette[12], palette[13], palette[14], palette[15] };
			}

			// else interpolate
			var interpolatedPalette = new Color[numColors];
			var toRasterSize = 1d / (numColors - 1);
			var fromRasterPos = 0;
			var fromRasterSize = 1d / (palette.Length - 1);
			for (var toRasterPos = 0; toRasterPos < numColors; toRasterPos++) {
				
				var fromColorPos = fromRasterPos * fromRasterSize;
				var toColorPos = toRasterPos * toRasterSize;
				var relativePos = (toColorPos - fromColorPos) / fromRasterSize;

				var fromColorStart = palette[fromRasterPos];
				var fromColorEnd = palette[fromRasterPos + 1];

				interpolatedPalette[toRasterPos] = MixColors(fromColorStart, fromColorEnd, relativePos);

				while (fromRasterSize * (fromRasterPos + 1) < toRasterSize * (toRasterPos + 1)) {
					fromRasterPos++;
				}
			}
			return interpolatedPalette;
		}

		/// <summary>
		/// Converts multiple colors to an RGB24 array.
		/// </summary>
		/// <param name="colors">Color to convert</param>
		/// <returns>Array of RGB bytes with a length of number of colors times 3</returns>
		public static byte[] ToByteArray(Color[] colors)
		{
			var arr = new byte[colors.Length * 3];
			var pos = 0;
			foreach (var color in colors) {
				arr[pos] = color.R;
				arr[pos + 1] = color.G;
				arr[pos + 2] = color.B;
				pos += 3;
			}
			return arr;
		}

		/// <summary>
		/// Converts a color to a single integer.
		/// </summary>
		/// <param name="color">Color to convert</param>
		/// <returns>>Color as single int, e.g. 0xff00ff</returns>
		public static int ToInt(Color color)
		{
			return (color.R << 16) + (color.G << 8) + color.B;
		}

		public static Color FromInt(int color) 
		{
			return Color.FromRgb(
				(byte)(color >> 16), 
				(byte)((color >> 8) & 0xff), 
				(byte)(color & 0xff)
			);
		}

		/// <summary>
		/// Converts a palette to an array of single integers.
		/// </summary>
		/// <param name="colors"></param>
		/// <seealso cref="ToInt"/>
		/// <returns>Array of single integers, one per color</returns>
		public static int[] ToIntArray(Color[] colors)
		{
			if (colors == null) {
				return Array.Empty<int>();
			}
			var arr = new int[colors.Length];
			var pos = 0;
			foreach (var color in colors) {
				arr[pos] = ToInt(color);
				pos++;
			}
			return arr;
		}

		/// <summary>
		/// Returns an RGB24 frame with colors from the palette applied to the frame.
		/// 
		/// Note that the size of the palette must be as large as the largest integer of 
		/// the frame to color, or in other words, the bit length is given by the size of
		/// the palette and the values of the frame.
		/// </summary>
		/// <param name="dim">Dimensions of the frame to color</param>
		/// <param name="frame">Frame to color, width * height pixels with values from 0 - [size of palette]</param>
		/// <param name="palette">Colors to use for coloring</param>
		/// <param name="bytesPerPixel">Number of output bytes per pixel. 3 for RGB24, 2 for RGB16.</param>
		/// <returns>Colorized frame</returns>
		/// <exception cref="ArgumentException">When provided frame and palette are incoherent</exception>
		public static byte[] ColorizeRgb(Dimensions dim, byte[] frame, Color[] palette, int bytesPerPixel)
		{
			using (Profiler.Start("ColorUtil.ColorizeRgb")) {
				#if DEBUG
				if (dim.Surface != frame.Length) {
					throw new ArgumentException("Data and dimensions do not match.");
				}
				#endif

				var frameLength = dim.Surface * 3;
				var colorizedData = new byte[frameLength];

				var palValues = new byte[bytesPerPixel][];
				for (var k = 0; k < bytesPerPixel; k++) {
					palValues[k] = new byte[palette.Length];
				}

				for (var i = 0; i < palette.Length; i++) {
					switch (bytesPerPixel) {
						case 3:
							palValues[0][i] = palette[i].R;
							palValues[1][i] = palette[i].G;
							palValues[2][i] = palette[i].B;
							break;

						case 2:
							var (x1, x2) = Rgb24ToRgb565(palette[i].R, palette[i].G, palette[i].B);
							palValues[0][i] = x1;
							palValues[1][i] = x2;
							break;

						default:
							throw new ArgumentException("Unsupported number of bytes per pixel.");
					}
				}

				var maxPixel = (byte)(palette.Length - 1);
				unsafe {
					fixed (byte* pFrame = frame, pcolorFrame = colorizedData) {
						byte* pFrameCur = pFrame, pFEnd = pFrame + frame.Length;
						byte* pColorFrameCur = pcolorFrame;

						for (; pFrameCur < pFEnd; pFrameCur++, pColorFrameCur += 3) {
							var pixel = *pFrameCur;
							if (pixel > maxPixel) {
								pixel = maxPixel; // Avoid crash when VPinMame sends data out of the palette range
							}
							for (var i = 0; i < bytesPerPixel; i++) {
								*(pColorFrameCur + i) = palValues[i][pixel];
							}
						}
					}
				}
				return colorizedData;
			}
		}

		/// <summary>
		/// Convert an RGB24 array to a RGB565 byte array.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="rgb888Data">RGB24 array, from top left to bottom right</param>
		/// <returns></returns>
		public static byte[] ConvertRgb24ToRgb565(Dimensions dim, byte[] rgb888Data)
		{
			var frame = new byte[dim.Surface * 2];
			var rgb565Pos = 0;
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width * 3; x += 3) {
					var rgb888Pos = y * dim.Width * 3 + x;
					Rgb24ToRgb565(rgb888Data, rgb888Pos, frame, rgb565Pos++);
				}
			}
			return frame;
		}

		/// <summary>
		/// Convert an RGB24 array to a RGB565 char array.
		/// </summary>
		/// <param name="dim">Dimensions of the image</param>
		/// <param name="rgb888Data">RGB24 array, from top left to bottom right</param>
		/// <param name="frame"></param>
		/// <returns></returns>
		public static char[] ConvertRgb24ToRgb565(Dimensions dim, byte[] rgb888Data, char[] frame)
		{
			var rgb565Pos = 0;
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width * 3; x += 3) {
					var rgb888Pos = y * dim.Width * 3 + x;
					Rgb24ToRgb565(rgb888Data, rgb888Pos, frame, rgb565Pos++);
				}
			}
			return frame;
		}

		private static void Rgb24ToRgb565(IReadOnlyList<byte> rgb888Data, int rgb888Pos, IList<byte> rgb565Data, int rgb565Pos)
		{
			var (x1, x2) = Rgb24ToRgb565(rgb888Data, rgb888Pos);
			rgb565Data[rgb565Pos] = x1;
			rgb565Data[rgb565Pos + 1] = x2;
		}

		private static void Rgb24ToRgb565(IReadOnlyList<byte> rgb888Data, int rgb888Pos, IList<char> rgb565Data, int rgb565Pos)
		{
			var (x1, x2) = Rgb24ToRgb565(rgb888Data, rgb888Pos);
			rgb565Data[rgb565Pos] = (char)((x1 << 8) + x2);
		}

		private static (byte, byte) Rgb24ToRgb565(IReadOnlyList<byte> rgb888Data, int rgb888Pos)
		{
			return Rgb24ToRgb565(rgb888Data[rgb888Pos], rgb888Data[rgb888Pos + 1], rgb888Data[rgb888Pos + 2]);
		}

		public static (byte, byte) Rgb24ToRgb565(byte r, byte g, byte b)
		{
			var x1 = (r & 0xF8) | (g >> 5);          // Take 5 bits of Red component and 3 bits of G component
			var x2 = ((g & 0x1C) << 3) | (b >> 3);   // Take remaining 3 Bits of G component and 5 bits of Blue component
			return ((byte)x1, (byte)x2);
		}

		/// <summary>
		/// Converts RGB565 frame data to an RGB24 array.
		/// </summary>
		/// <param name="dim">Dimensions of the array</param>
		/// <param name="rgb565Data">RGB565 data of the frame</param>
		/// <returns>Converted RGB888 data</returns>
		public static byte[] ConvertRgb565ToRgb24(Dimensions dim, byte[] rgb565Data)
		{
			var rgb888Data = new byte[dim.Surface * 3];
			for (var x = 0; x < dim.Width; x++) {
				for (var y = 0; y < dim.Height; y++) {
					var rgb565Pos = (y * dim.Width + x) * 2;
					var rgb888Pos = (y * dim.Width + x) * 3;
					Rgb565ToRgb888(rgb565Data, rgb565Pos, rgb888Data, rgb888Pos);
				}
			}
			return rgb888Data;
		}

		/// <summary>
		/// Converts a RGB565 pixel of a frame to a pixel in a RGB24 frame.
		/// </summary>
		/// <param name="rgb565Data">Source RGB565 array</param>
		/// <param name="rgb565Pos">Position of the source pixel within the array (not the frame)</param>
		/// <param name="rgb888Data">Destination RGB888 array</param>
		/// <param name="rgb888Pos">Position of the destination pixel without the array (not the frame)</param>
		private static void Rgb565ToRgb888(IReadOnlyList<byte> rgb565Data, int rgb565Pos, IList<byte> rgb888Data, int rgb888Pos)
		{
			var (r, g, b) = Rgb565ToRgb888(rgb565Data, rgb565Pos);
			rgb888Data[rgb888Pos] = r;
			rgb888Data[rgb888Pos + 1] = g;
			rgb888Data[rgb888Pos + 2] = b;
		}

		/// <summary>
		/// Returns the RGB888 values of a pixel within a RGB565 array.
		/// </summary>
		/// <param name="rgb565Data">RGB555 array</param>
		/// <param name="rgb565Pos">Position of the pixel within the array (not the frame)</param>
		/// <returns>A triplet of RGB values</returns>
		public static (byte, byte, byte) Rgb565ToRgb888(IReadOnlyList<byte> rgb565Data, int rgb565Pos)
		{
			var rgb565 = GetRgb565Pixel(rgb565Data, rgb565Pos);
			return (
				(byte)(((rgb565 >> 8) & 0xF8) | ((rgb565 >> 13) & 0x07)), // shifting then copying the 3 most significant bits to the right
				(byte)(((rgb565 >> 3) & 0xFC) | ((rgb565 >> 9) & 0x03)), // shifting then copying the 2 most significant bits to the right
				(byte)(((rgb565 << 3) & 0xF8) | ((rgb565 >> 2) & 0x07)) // shifting then copying the 3 most significant bits to the right
			);
		}

		/// <summary>
		/// Returns a ushort RGB565 value from a byte array at a given position
		/// </summary>
		/// <param name="rgb565Data">RGB565 array</param>
		/// <param name="position">Position within the array</param>
		/// <returns></returns>
		private static ushort GetRgb565Pixel(IReadOnlyList<byte> rgb565Data, int position)
		{
			// for some reason, those are inverted
			return (ushort)((rgb565Data[position + 1] << 8) | rgb565Data[position]);
		}

		/// <summary>
		/// Mixes two colors in a give proportion
		/// </summary>
		/// <param name="color1">First color</param>
		/// <param name="color2">Second color</param>
		/// <param name="p">Proportion. The higher, the more of color2 will be returned. Must between 0 and 1.</param>
		/// <returns></returns>
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
