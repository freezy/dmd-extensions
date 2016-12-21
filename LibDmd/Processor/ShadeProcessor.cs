using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Output;
using NLog;

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
		/// <see cref="Brightness"/>.
		/// </summary>
		/// <remarks>Set to 0 to disable shading (only intensity and lightness apply).</remarks>
		public int NumShades { get; set; } = 4;

		/// <summary>
		/// Overrides the luminosity per shade.
		/// </summary>
		public double[] Shades { get; set; } = null;

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
		public double Brightness { get; set; } = 0;


		public override BitmapSource Process(BitmapSource bmp, IDestination dest)
		{
//			var luminosities = new HashSet<double>();
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

					var num = NumShades;
					if (dest.IsRgb && Shades != null && Shades.Length == NumShades) {
						var index = (int)Math.Min(num - 1, Math.Max(0, (Math.Round(luminosity * Intensity * num) + Brightness)));
						luminosity = Shades[index];
					} else {
						luminosity = Math.Max(0, (Math.Round(luminosity * Intensity * num) + Brightness) / num);
					}


				} else {
					luminosity = Math.Max(0, luminosity * Intensity + Brightness);
				}
//				luminosities.Add(luminosity);

				// convert back to RGB
				byte red;
				byte green;
				byte blue;
				saturation = 1;
				ColorUtil.HslToRgb(hue, saturation, luminosity, out red, out green, out blue);

				pixelBuffer[k] = blue;
				pixelBuffer[k + 1] = green;
				pixelBuffer[k + 2] = red;
			}
//			Console.WriteLine(string.Join(",", luminosities));

			var wBmp = new WriteableBitmap(bmp);
			wBmp.WritePixels(fullRect, pixelBuffer, stride, 0);
			wBmp.Freeze();

			_whenProcessed.OnNext(wBmp);
			return wBmp;
		}
	}
}
