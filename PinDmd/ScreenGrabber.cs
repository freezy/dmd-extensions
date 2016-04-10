using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PinDmd
{
	public class ScreenGrabber
	{
		public int Width { get; set; }
		public int Height { get; set; }

		public ScreenGrabber(int width, int height)
		{
			Width = width;
			Height = height;
		}

		public Bitmap Grab(int left, int top)
		{
			var sw = new Stopwatch();
			sw.Start();
			var image = NativeMethods.GetDesktopBitmap(left, top, Width, Height);
			var bmp = new Bitmap(image);
			sw.Stop();
			Console.WriteLine("Grabbed screen at {0}/{1} at {2}x{3} in {4}ms", left, top, Width, Height, sw.ElapsedMilliseconds);
			return bmp;
		}

	}
}
