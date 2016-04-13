using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PinDmd.Processor
{
	public class GridProcessor : AbstractProcessor
	{
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;
		public double Padding { get; set; }

		public override BitmapSource Process(BitmapSource bmp)
		{
			var sw = new Stopwatch();
			sw.Start();

			var sliceWidth = bmp.Width / (Width + Width * Padding - Padding);
			var sliceHeight = bmp.Height / (Height + Height * Padding - Padding);
			var paddingWidth = (bmp.Width * Padding) / (Width + Width * Padding - Padding);
			var paddingHeight = (bmp.Height * Padding) / (Height + Height * Padding - Padding);
			var destRect = new Int32Rect();
			var srcRect = new Int32Rect();
			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;

			var dest = new WriteableBitmap(bmp);

			srcRect.Y = 0;
			srcRect.Width = (int)Math.Ceiling(sliceWidth);
			srcRect.Height = bmp.PixelHeight;
			destRect.Y = 0;
			destRect.Width = srcRect.Width;
			destRect.Height = bmp.PixelHeight;
			var blockSize = bytesPerPixel * srcRect.Width * bmp.PixelHeight;
			var stride = srcRect.Width * bytesPerPixel;
			var buffer = new byte[blockSize];
			for (var x = 0; x < Width; x++) {
				srcRect.X = (int)(x * (sliceWidth + paddingWidth));
				destRect.X = (int)(x * sliceWidth);
				bmp.CopyPixels(srcRect, buffer, stride, 0);
				dest.WritePixels(destRect, buffer, stride, 0);
			}

			srcRect.X = 0;
			srcRect.Width = bmp.PixelWidth;
			srcRect.Height = (int)Math.Ceiling(sliceHeight);
			destRect.X = 0;
			destRect.Width = bmp.PixelWidth;
			destRect.Height = srcRect.Height;
			blockSize = bytesPerPixel * bmp.PixelWidth * srcRect.Height;
			stride = bmp.PixelWidth * bytesPerPixel;
			buffer = new byte[blockSize];
			for (var y = 0; y < Height; y++) {
				srcRect.Y = (int)(y * (sliceHeight + paddingHeight));
				destRect.Y = (int)(y * sliceHeight);
				dest.CopyPixels(srcRect, buffer, stride, 0);
				dest.WritePixels(destRect, buffer, stride, 0);
			}

			var img = new CroppedBitmap(dest, new Int32Rect(0, 0, (int)(sliceWidth * Width), (int)(sliceHeight * Height)));

			sw.Stop();
			//Console.WriteLine("Grid-sized from {0}x{1} to {2}x{3} in {4}ms.", bmp.Width, bmp.Height, img.PixelWidth, img.PixelHeight, sw.ElapsedMilliseconds);

			img.Freeze();
			_whenProcessed.OnNext(img);
			return img;
		}

		public static void Dump(BitmapSource image, string filePath)
		{
			using (var fileStream = new FileStream(filePath, FileMode.Create)) {
				BitmapEncoder encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(image));
				encoder.Save(fileStream);
			}
		}

	}
}
