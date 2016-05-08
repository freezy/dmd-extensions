using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Output;

namespace LibDmd.Processor
{
	/// <summary>
	/// Resizes or flips a frame to given dimensions.
	/// </summary>
	public class TransformationProcessor : AbstractProcessor
	{
		/// <summary>
		/// Pixel width of the output frame
		/// </summary>
		public int Width { get; set; } = 128;

		/// <summary>
		/// Pixel height of the output frame.
		/// </summary>
		public int Height { get; set; } = 32;

		/// <summary>
		/// If set, flips the image vertically.
		/// </summary>
		public bool FlipVertically { get; set; }

		/// <summary>
		/// If set, flips the image horizontally.
		/// </summary>
		public bool FlipHorizontally { get; set; }

		/// <summary>
		/// How the image is resized
		/// </summary>
		public ResizeMode Resize { get; set; } = ResizeMode.Fit;

		public override BitmapSource Process(BitmapSource bmp, IFrameDestination dest)
		{
			if (bmp.PixelWidth == Width && bmp.PixelHeight == Height && !FlipHorizontally && !FlipVertically) {
				return bmp;
			}
			var sw = new Stopwatch();
			sw.Start();

			var srcAr = (double)bmp.PixelWidth / bmp.PixelHeight;
			var destAr = (double)Width / Height;

			double width;
			double height;
			var crop = false;
			var fit = false;

			const double tolerance = 0.01;

			// resize to fill
			if (Resize == ResizeMode.Fill && Math.Abs(destAr - srcAr) > tolerance) {
				if (destAr > srcAr) {
					width = Width;
					height = Width / srcAr;
				} else {
					width = Height * srcAr;
					height = Height;
				}
				crop = true;

			// resize to fit
			} else if (Resize == ResizeMode.Fit && Math.Abs(destAr - srcAr) > tolerance) {
				if (destAr > srcAr) {
					width = Height * srcAr;
					height = Height;
					
				} else {
					width = Width;
					height = Width / srcAr;
				}
				fit = true;

			// otherwise, stretch.
			} else {
				width = Width;
				height = Height;
			}

			BitmapSource processedBmp = new TransformedBitmap(bmp, new ScaleTransform(width / bmp.PixelWidth * (FlipHorizontally ? -1 : 1), height / bmp.PixelHeight * (FlipVertically ? -1 : 1), (double)bmp.PixelWidth / 2, (double)bmp.PixelHeight / 2));

			// filled needs cropping
			if (crop) {
				var cropParams = new Int32Rect(0, 0, Width, Height);
				if (destAr > srcAr) {
					cropParams.X = 0;
					cropParams.Y = (int)((height - Height) / 2);
				} else {
					cropParams.X = (int)((width - Width) / 2);
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
				var emptyBmp = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgr32, bmp.Palette);

				// copy resized bitmap to new canvas
				var rect = new Int32Rect(0, 0, processedBmp.PixelWidth, processedBmp.PixelHeight);
				processedBmp.CopyPixels(rect, buffer, stride, 0);
				if (destAr > srcAr) {
					rect.X = (Width - processedBmp.PixelWidth) / 2;
					rect.Y = 0;
				} else {
					rect.X = 0;
					rect.Y = (Height - processedBmp.PixelHeight) / 2;
				}
				emptyBmp.WritePixels(rect, buffer, stride, 0);
				processedBmp = emptyBmp;
			}

			processedBmp.Freeze();
			_whenProcessed.OnNext(processedBmp);
			return processedBmp;
		}

		public enum ResizeMode
		{
			/// <summary>
			/// Stretch to fit dimensions. Aspect ratio is not kept.
			/// </summary>
			Stretch,

			/// <summary>
			/// Smaller dimensions fits while larger dimension gets cropped.
			/// </summary>
			Fill,

			/// <summary>
			/// Larger dimensions fits and smaller dimension stays black.
			/// </summary>
			Fit
		}
	}
}
