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
	public class ResizeProcessor : IProcessor
	{
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;

		public bool Enabled { get; set; } = true;

		public BitmapSource Process(BitmapSource bmp)
		{
			if (bmp.PixelWidth == Width && bmp.PixelHeight == Height) {
				return bmp;
			}
			var sw = new Stopwatch();
			sw.Start();
			var resized = new TransformedBitmap(bmp, new ScaleTransform(Width / bmp.Width, Height / bmp.Height, 0, 0));
			return BitmapFrame.Create(resized);
		}
	}
}
