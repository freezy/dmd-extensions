using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;

namespace PinDmd.Processor
{
	public class ResizeProcessor : IProcessor
	{
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;

		public bool Enabled { get; set; } = true;

		public Bitmap Process(Bitmap bmp)
		{
			if (bmp.Width == Width && bmp.Height == Height) {
				return bmp;
			}

			var sw = new Stopwatch();
			sw.Start();
			var result = new Bitmap(Width, Height);
			using (var g = Graphics.FromImage(result)) {
				g.DrawImage(bmp, 0, 0, Width, Height);
			}
			sw.Stop();
			//Console.WriteLine("Resized from {0}x{1} to {2}x{3} in {4}ms.", bmp.Width, bmp.Height, result.Width, result.Height, sw.ElapsedMilliseconds);
			return result;
		}
	}
}
