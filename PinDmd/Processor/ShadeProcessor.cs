using System;
using System.Collections.Generic;
using System.Linq;
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
				var b = pixelBuffer[k];
				var g = pixelBuffer[k + 1];
				var r = pixelBuffer[k + 2];
					
				var luminosity = (byte)((0.299 * r + 0.587 * g + 0.114 * b) * Intensity);
				var shadedLuminosity = (Math.Round((double)luminosity/255* NumShades) + Lightness) / NumShades * 255;

				var blue = shadedLuminosity;
				var green = shadedLuminosity;
				var red = shadedLuminosity;

				if (blue < 0) { blue = 0; }
				if (green < 0) { green = 0; }
				if (red < 0) { red = 0; }

				pixelBuffer[k] = (byte)blue;
				pixelBuffer[k + 1] = (byte)green;
				pixelBuffer[k + 2] = (byte)red;
			}

			var dest = new WriteableBitmap(bmp);
			dest.WritePixels(fullRect, pixelBuffer, stride, 0);
			dest.Freeze();

			_whenProcessed.OnNext(dest);
			return dest;
		}
	}
}
