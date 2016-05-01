using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Output;

namespace LibDmd.Processor
{
	/// <summary>
	/// A processor that strips off the space between the "dot grid" of
	/// a rendered DMD frame.
	/// </summary>
	public class DistortionProcessor : AbstractProcessor
	{
		public override bool Enabled
		{
			get { return _enabled && Distortion > 0; }
			set { _enabled = value; }
		}

		/// <summary>
		/// Number of horizontal dots
		/// </summary>
		public int Width { get; set; } = 128;

		/// <summary>
		/// Number of vertical dots
		/// </summary>
		public int Height { get; set; } = 32;

		/// <summary>
		/// Top cut-off of the trapezoid. 1 results in a triangle, 0 in a rectangle
		/// </summary>
		public double Distortion { get; set; }

		private bool _enabled = true;

		public override BitmapSource Process(BitmapSource bmp, IFrameDestination dest)
		{
			var sw = new Stopwatch();
			sw.Start();

			var sliceWidth = bmp.Width / Width;
			var sliceHeight = bmp.Height / Height;
			var destRect = new Int32Rect();
			var srcRect = new Int32Rect();
			var lineRect = new Int32Rect();
			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;

			var wBmp = new WriteableBitmap(bmp);

			srcRect.Height = 1;
			destRect.X = 0;
			destRect.Width = bmp.PixelWidth;
			destRect.Height = srcRect.Height;
			lineRect.Height = 1;
			lineRect.X = 0;
			lineRect.Width = bmp.PixelWidth;
			var blockSize = bytesPerPixel * bmp.PixelWidth * srcRect.Height;
			var stride = bmp.PixelWidth * bytesPerPixel;
			var buffer = new byte[blockSize];
			var line = new WriteableBitmap(bmp.PixelWidth, 1, bmp.DpiX, bmp.DpiY, bmp.Format, bmp.Palette);

			for (var y = 0; y < bmp.PixelHeight; y++)
			{
				// copy line
				lineRect.Y = y;
				bmp.CopyPixels(lineRect, buffer, stride, 0);
				lineRect.Y = 0;
				line.WritePixels(lineRect, buffer, stride, 0);

				// calculate scaling
				var deltaAbs = (double)bmp.PixelWidth / 2 * Distortion;
				var deltaRel = deltaAbs * (1 - (double)y / bmp.PixelHeight);
				srcRect.Width = bmp.PixelWidth - (int)(2 * deltaRel);
				srcRect.X = (int)deltaRel;
				srcRect.Y = y;
				destRect.Y = y;
				var scaleX = (double)bmp.PixelWidth / srcRect.Width;

				// scale line
				var scaledLine = new TransformedBitmap(line, new ScaleTransform(scaleX, 1, (double)line.PixelWidth / 2, 0));

				// copy scaled line to dest
				lineRect.X = (int)((bmp.PixelWidth*scaleX) - bmp.PixelWidth)/2;
				scaledLine.CopyPixels(lineRect, buffer, stride, 0);
				lineRect.X = 0;
				lineRect.Y = y;
				wBmp.WritePixels(lineRect, buffer, stride, 0);
			}

			sw.Stop();
			Console.WriteLine("Distored image in {0}ms.", sw.ElapsedMilliseconds);

			wBmp.Freeze();
			_whenProcessed.OnNext(wBmp);
			return wBmp;
		}
	}
}
