using System;
using System.Windows;
using System.Windows.Media.Imaging;
using LibDmd.Common;

namespace LibDmd.Processor
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
				ColorUtil.RgbToHsl(r, g, b, out hue, out saturation, out luminosity);

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
				ColorUtil.HslToRgb(hue, saturation, luminosity, out red, out green, out blue);

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
	}
}
