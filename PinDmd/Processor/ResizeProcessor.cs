using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;

namespace PinDmd.Processor
{
	/// <summary>
	/// Resizes a frame to given dimensions.
	/// </summary>
	public class ResizeProcessor : AbstractProcessor
	{
		/// <summary>
		/// Pixel width of the output frame
		/// </summary>
		public int Width { get; set; } = 128;

		/// <summary>
		/// Pixel height of the output frame.
		/// </summary>
		public int Height { get; set; } = 32;

		public override BitmapSource Process(BitmapSource bmp)
		{
			if (bmp.PixelWidth == Width && bmp.PixelHeight == Height) {
				return bmp;
			}
			var sw = new Stopwatch();
			sw.Start();
			var resized = new TransformedBitmap(bmp, new ScaleTransform(Width / bmp.Width, Height / bmp.Height, 0, 0));
			var dest = BitmapFrame.Create(resized);
			dest.Freeze();

			_whenProcessed.OnNext(dest);
			return dest;
		}
	}
}
