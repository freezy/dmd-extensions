using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Windows.Media.PixelFormat;

namespace LibDmd.Processor
{
	/// <summary>
	/// Converts a frame to monochrome, with optional color tinting.
	/// </summary>
	public class MonochromeProcessor : AbstractProcessor
	{
		/// <summary>
		/// Color to tint. Set to <see cref="Colors.Transparent"/> for no tinting.
		/// </summary>
		public Color Tint { get; set; }

		/// <summary>
		/// Pixel format. Settings this to <see cref="PixelFormats.Gray2"/> will not
		/// give good results. Use another processor for fixed shades.
		/// </summary>
		public PixelFormat PixelFormat { get; set; } = PixelFormats.Gray16;

		public override bool IsGreyscaleCompatible { get; } = false;

		public override BitmapSource Process(BitmapSource bmp)
		{
			var monochrome = new FormatConvertedBitmap();

			monochrome.BeginInit();
			monochrome.Source = bmp;
			monochrome.DestinationFormat = PixelFormat;
			monochrome.EndInit();

			var dest = Tint.A > 0 ? ColorShade(monochrome, Tint) : monochrome;
			_whenProcessed.OnNext(dest);

			return dest;
		}

		public static BitmapSource ColorShade(BitmapSource bmp, Color color)
		{
			// convert back to rgb24
			var colored = new FormatConvertedBitmap();
			colored.BeginInit();
			colored.Source = bmp;
			colored.DestinationFormat = PixelFormats.Bgr32;
			colored.EndInit();

			var bytesPerPixel = (colored.Format.BitsPerPixel + 7) / 8;
			var stride = colored.PixelWidth * bytesPerPixel;
			var pixelBuffer = new byte[stride * colored.PixelHeight];
			var fullRect = new Int32Rect { X = 0, Y = 0, Width = colored.PixelWidth, Height = colored.PixelHeight };
			
			colored.CopyPixels(fullRect, pixelBuffer, stride, 0);

			for (var k = 0; k + 4 < pixelBuffer.Length; k += 4) {
				var blue = pixelBuffer[k] * color.ScB;
				var green = pixelBuffer[k + 1] * color.ScG;
				var red = pixelBuffer[k + 2] * color.ScR;

				if (blue < 0) { blue = 0; }
				if (green < 0) { green = 0; }
				if (red < 0) { red = 0; }

				pixelBuffer[k] = (byte)blue;
				pixelBuffer[k + 1] = (byte)green;
				pixelBuffer[k + 2] = (byte)red;
			}

			var dest = new WriteableBitmap(colored);
			dest.WritePixels(fullRect, pixelBuffer, stride, 0);
			dest.Freeze();

			return dest;
		}
	}
}
