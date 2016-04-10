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

		public Bitmap Grab2(int left, int top)
		{
			var sw = new Stopwatch();
			sw.Start();
			var image = NativeMethods.GetDesktopBitmap(left, top, Width, Height);
			var bmp = new Bitmap(image);
			sw.Stop();
			Console.WriteLine("Grabbed screen at {0}/{1} at {2}x{3} in {4}ms", left, top, Width, Height, sw.ElapsedMilliseconds);
			//bmp.Save(@"grabbed.png");
			return bmp;
		}

		public Bitmap Grab(int left, int top)
		{
			var sw = new Stopwatch();
			sw.Start();
			left += 200;
			top += 50;
			var hDesk = GetDesktopWindow();
			var hSrce = GetWindowDC(hDesk);
			var hDest = CreateCompatibleDC(hSrce);
			var hBmp = CreateCompatibleBitmap(hSrce, Width, Height);
			var hOldBmp = SelectObject(hDest, hBmp);
			BitBlt(hDest, left, top, Width, Height, hSrce, 0, 0, CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
			var bmp = Image.FromHbitmap(hBmp);
			SelectObject(hDest, hOldBmp);
			DeleteObject(hBmp);
			DeleteDC(hDest);
			ReleaseDC(hDesk, hSrce);
			sw.Stop();
			Console.WriteLine("Grabbed screen at {0}/{1} at {2}x{3} in {4}ms", left, top, Width, Height, sw.ElapsedMilliseconds);

			bmp.Save(@"grabbed.png");
			return bmp;
		}

		#region P/Invoke declarations
		[DllImport("gdi32.dll")]
		static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int
		wDest, int hDest, IntPtr hdcSource, int xSrc, int ySrc, CopyPixelOperation rop);
		[DllImport("user32.dll")]
		static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDc);
		[DllImport("gdi32.dll")]
		static extern IntPtr DeleteDC(IntPtr hDc);
		[DllImport("gdi32.dll")]
		static extern IntPtr DeleteObject(IntPtr hDc);
		[DllImport("gdi32.dll")]
		static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
		[DllImport("gdi32.dll")]
		static extern IntPtr CreateCompatibleDC(IntPtr hdc);
		[DllImport("gdi32.dll")]
		static extern IntPtr SelectObject(IntPtr hdc, IntPtr bmp);
		[DllImport("user32.dll")]
		public static extern IntPtr GetDesktopWindow();
		[DllImport("user32.dll")]
		public static extern IntPtr GetWindowDC(IntPtr ptr);

		#endregion
	}
}
