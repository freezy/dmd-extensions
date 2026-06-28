using System;
using LibDmd.Frame;

namespace LibDmd.Common
{
	/// <summary>
	/// The cross-platform (bitmap-free) half of <see cref="ImageUtil"/>.
	///
	/// These methods operate purely on byte arrays and are compiled into the
	/// cross-platform core (LibDmd.Core). The WPF/<c>System.Drawing</c> bitmap half
	/// stays in <c>ImageUtil.cs</c>, which the core excludes.
	/// </summary>
	public static partial class ImageUtil
	{
		/// <summary>
		/// Converts an RGB24 frame to a grayscale array.
		/// </summary>
		/// <param name="dim">Pixel dimensions</param>
		/// <param name="frameRgb24">RGB24 frame, top left to bottom right, three bytes per pixel with values between 0 and 255</param>
		/// <param name="numColors">Number of gray tones. 4 for 2 bit, 16 for 4 bit</param>
		/// <returns>Gray2 frame, top left to bottom right, one byte per pixel with values between 0 and 3</returns>
		public static byte[] ConvertToGray(Dimensions dim, byte[] frameRgb24, int numColors)
		{
			using (Profiler.Start("ImageUtil.ConvertToGray")) {

				var frame = new byte[dim.Surface];
				var pos = 0;
				for (var y = 0; y < dim.Height; y++) {
					for (var x = 0; x < dim.Width * 3; x += 3) {
						var rgbPos = y * dim.Width * 3 + x;

						// convert to HSL
						ColorUtil.RgbToHsl(frameRgb24[rgbPos], frameRgb24[rgbPos + 1], frameRgb24[rgbPos + 2], out _, out _, out var luminosity);
						frame[pos++] = (byte)Math.Round(luminosity * (numColors - 1));
					}
				}
				return frame;
			}
		}

		public static byte[] ConvertRgb565ToGray(Dimensions dim, byte[] rgb565Data, int numColors)
		{
			using (Profiler.Start("ImageUtil.ConvertRgb565ToGray")) {

				var frame = new byte[dim.Surface];
				var pos = 0;
				for (var y = 0; y < dim.Height; y++) {
					for (var x = 0; x < dim.Width; x++) {
						var i = (y * dim.Width + x) * 2;
						var (r, g, b) = ColorUtil.Rgb565ToRgb888(rgb565Data, i);

						// convert to HSL
						ColorUtil.RgbToHsl(r, g, b, out _, out _, out var luminosity);
						frame[pos++] = (byte)Math.Round(luminosity * (numColors - 1));
					}
				}
				return frame;
			}
		}

		public static void ConvertRgb24ToBgr32(Dimensions dim, byte[] from, byte[] to)
		{
			var pos = 0;
			for (var y = 0; y < dim.Height; y++) {
				for (var x = 0; x < dim.Width * 3; x += 3) {
					var fromPos = dim.Width * 3 * y + x;
					to[pos] = from[fromPos + 2];
					to[pos + 1] = from[fromPos + 1];
					to[pos + 2] = from[fromPos];
					to[pos + 3] = 0;
					pos += 4;
				}
			}
		}

		/// <summary>
		/// Get pixel color of the frame data
		/// </summary>
		public static byte GetPixel(int x, int y, int width, int height, byte[] frame)
		{
			// Clamp edges so it doesn't wrap.
			x = Clamp(x, 0, width - 1);
			y = Clamp(y, 0, height - 1);

			return frame[x + (width * y)];
		}

		/// <summary>
		/// Set pixel color of a texture block
		/// </summary>
		public static void SetPixel(int x, int y, byte color, int width, byte[] frame)
		{
			frame[x + (width * y)] = color;
		}

		/// <summary>
		/// Get pixel color of the RGB frame data
		/// </summary>
		public static void GetRgbPixel(int x, int y, int width, int height, byte[] frame, byte[] rgb)
		{
			// Clamp edges so it doesn't wrap.
			x = Clamp(x, 0, width - 1);
			y = Clamp(y, 0, height - 1);

			for (var i = 0; i < rgb.Length; i++) {
				rgb[i] = frame[x * rgb.Length + i + (width * rgb.Length * y)];
			}
		}

		/// <summary>
		/// Set pixel RGB color of a texture block
		/// </summary>
		public static void SetRgbPixel(int x, int y, byte[] color, int width, byte[] frame, int bytesPerPixel)
		{
			for (var i = 0; i < bytesPerPixel; i++) {
				frame[x * bytesPerPixel + i + (width * bytesPerPixel * y)] = color[i];
			}
		}

		/// <summary>
		/// Clamp values
		/// </summary>
		public static int Clamp(int value, int min, int max)
		{
			return (value < min) ? min : (value > max) ? max : value;
		}
	}
}
