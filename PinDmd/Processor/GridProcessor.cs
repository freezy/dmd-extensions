using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PinDmd.Processor
{
	public class GridProcessor : IProcessor
	{
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;
		public double Padding { get; set; }

		public bool Enabled { get; set; } = true;

		public Bitmap Process(Bitmap bmp)
		{
			var sw = new Stopwatch();
			sw.Start();

			var sliceWidth = (float)(bmp.Width / (Width + Width * Padding - Padding));
			var sliceHeight = (float)(bmp.Height / (Height + Height * Padding - Padding));
			var paddingWidth = (bmp.Width * Padding) / (Width + Width*Padding - Padding);
			var paddingHeight = (bmp.Height * Padding) / (Height + Height * Padding - Padding);
			var destRect = new Rectangle(0, 0, 1, 1);

			var result = new Bitmap(Width, Height);
			using (var g = Graphics.FromImage(result)) {
				for (var x = 0; x < Width; x++) {
					for (var y = 0; y < Height; y++) {
						destRect.X = x;
						destRect.Y = y;
						g.DrawImage(bmp,
							destRect, 
							(float)(x * (sliceWidth + paddingWidth)), 
							(float)(y * (sliceHeight + paddingHeight)), 
							sliceWidth, sliceHeight, GraphicsUnit.Pixel);
					}
				}
			}

			sw.Stop();
			//Console.WriteLine("Resized from {0}x{1} to {2}x{3} in {4}ms.", bmp.Width, bmp.Height, result.Width, result.Height, sw.ElapsedMilliseconds);
			return result;
		}
	}
}
