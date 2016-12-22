using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Output;
using ResizeMode = LibDmd.Input.ResizeMode;

namespace LibDmd.Common
{
	public class TransformationUtil
	{

		public static BitmapSource Transform(BitmapSource bmp, int destWidth, int destHeight, ResizeMode resize, bool flipHorizontally, bool flipVertically)
		{
			if (bmp.PixelWidth == destWidth && bmp.PixelHeight == destHeight && !flipHorizontally && !flipVertically) {
				return bmp;
			}
			Console.WriteLine("Transforming from {0}x{1} to {2}x{3}...", bmp.PixelWidth, bmp.PixelHeight, destWidth, destHeight);

			var sw = new Stopwatch();
			sw.Start();

			var srcAr = (double)bmp.PixelWidth / bmp.PixelHeight;
			var destAr = (double)destWidth / destHeight;

			double width;
			double height;
			var crop = false;
			var fit = false;

			const double tolerance = 0.01;

			// resize to fill
			if (resize == ResizeMode.Fill && Math.Abs(destAr - srcAr) > tolerance) {
				if (destAr > srcAr) {
					width = destWidth;
					height = destWidth / srcAr;
				} else {
					width = destHeight * srcAr;
					height = destHeight;
				}
				crop = true;

			// resize to fit
			} else if (resize == ResizeMode.Fit && Math.Abs(destAr - srcAr) > tolerance) {
				if (destAr > srcAr) {
					width = destHeight * srcAr;
					height = destHeight;
					
				} else {
					width = destWidth;
					height = destWidth / srcAr;
				}
				fit = true;

			// otherwise, stretch.
			} else {
				width = destWidth;
				height = destHeight;
			}

			BitmapSource processedBmp = new TransformedBitmap(bmp, new ScaleTransform(width / bmp.PixelWidth * (flipHorizontally ? -1 : 1), height / bmp.PixelHeight * (flipVertically ? -1 : 1), (double)bmp.PixelWidth / 2, (double)bmp.PixelHeight / 2));

			// filled needs cropping
			if (crop) {
				var cropParams = new Int32Rect(0, 0, destWidth, destHeight);
				if (destAr > srcAr) {
					cropParams.X = 0;
					cropParams.Y = (int)((height - destHeight) / 2);
				} else {
					cropParams.X = (int)((width - destWidth) / 2);
					cropParams.Y = 0;
				}
				processedBmp = new CroppedBitmap(processedBmp, cropParams);
			}

			// fit needs painting on new canvas
			if (fit) {
				var bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
				var blockSize = bytesPerPixel * processedBmp.PixelWidth * processedBmp.PixelHeight;
				var buffer = new byte[blockSize];
				var stride = processedBmp.PixelWidth * bytesPerPixel;

				// create new canvas
				var emptyBmp = new WriteableBitmap(destWidth, destHeight, 96, 96, PixelFormats.Bgr32, bmp.Palette);

				// copy resized bitmap to new canvas
				var rect = new Int32Rect(0, 0, processedBmp.PixelWidth, processedBmp.PixelHeight);
				processedBmp.CopyPixels(rect, buffer, stride, 0);
				if (destAr > srcAr) {
					rect.X = (destWidth - processedBmp.PixelWidth) / 2;
					rect.Y = 0;
				} else {
					rect.X = 0;
					rect.Y = (destHeight - processedBmp.PixelHeight) / 2;
				}
				emptyBmp.WritePixels(rect, buffer, stride, 0);
				processedBmp = emptyBmp;
			}

			processedBmp.Freeze();
			return processedBmp;
		}
	}
}
