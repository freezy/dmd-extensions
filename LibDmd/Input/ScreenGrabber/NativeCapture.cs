using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Drawing.Point;

namespace LibDmd.Input.ScreenGrabber
{
	/// <summary>
	/// Class for getting images of the desktop.
	/// </summary>
	/// <threadsafety static="false" instance="false"/>
	/// <note type="caution">This class is not thread safe.</note> 
	/// <remarks>This class has been scaled back to the essentials for capturing a segment of 
	/// the desktop in order to keep Cropper as small as possible.</remarks>
	internal static class NativeCapture
	{
		#region Dll Imports

		[DllImport("user32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)]
		internal static extern IntPtr GetDC(IntPtr hwnd);

		[DllImport("gdi32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)]
		internal static extern bool DeleteDC(IntPtr hwnd);

		[DllImport("gdi32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, Int32 dwRop);

		[DllImport("user32.dll")]
		internal static extern int GetWindowRgn(IntPtr hWnd, IntPtr hRgn);

		[DllImport("user32.dll", SetLastError = true)]
		internal static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nReghtRect, int nBottomRect);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nReghtRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

		[DllImport("user32.dll")]
		internal static extern ulong GetWindowLongA(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", SetLastError = true)]
		internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		#endregion

		#region Fields

		private const int SRCCOPY = 0x00CC0020;
		private const int CAPTUREBLT = 1073741824;
		internal const int ECM_FIRST = 0x1500;
		private const int GWL_STYLE = -16;
		private const ulong WS_VISIBLE = 0x10000000L;
		private const ulong WS_BORDER = 0x00800000L;
		private const ulong TARGETWINDOW = WS_BORDER | WS_VISIBLE;
		//private const Int32 CURSOR_SHOWING = 0x00000001;

		internal const Int32 WM_USER = 0x0400;

		internal const Int32 HKM_SETHOTKEY = (WM_USER + 1);
		internal const Int32 HKM_GETHOTKEY = (WM_USER + 2);
		internal const Int32 HKM_SETRULES = (WM_USER + 3);
		internal const Int32 HOTKEYF_SHIFT = 0x01;
		internal const Int32 HOTKEYF_CONTROL = 0x02;
		internal const Int32 HOTKEYF_ALT = 0x04;
		internal const Int32 HOTKEYF_EXT = 0x08;
		internal const String HOTKEY_CLASS = "msctls_hotkey32";

		internal const Int32 MAPVK_VK_TO_VSC = 0;
		internal const Int32 MAPVK_VSC_TO_VK = 1;
		internal const Int32 MAPVK_VK_TO_CHAR = 2;
		internal const Int32 MAPVK_VSC_TO_VK_EX = 3;
		internal const uint KLF_NOTELLSHELL = 0x00000080;

		#endregion

		#region Structures

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public readonly int Left;
			public readonly int Top;
			public readonly int Right;
			public readonly int Bottom;

			public int Width => Right - Left;
			public int Height => Bottom - Top;

			public RECT(int left, int top, int right, int bottom)
			{
				Left = left;
				Top = top;
				Right = right;
				Bottom = bottom;
			}

			public Rectangle ToRectangle()
			{
				return new Rectangle(Left, Top, Right - Left, Bottom - Top);
			}
		}


		[StructLayout(LayoutKind.Sequential)]
		internal struct POINT
		{
			public int X;
			public int Y;

			public POINT(int x, int y)
			{
				X = x;
				Y = y;
			}

			public static explicit operator POINT(Point pt)
			{
				return new POINT(pt.X, pt.Y);
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Gets a segment of the desktop as an image.
		/// </summary>
		/// <param name="rectangle">The rectangular area to capture.</param>
		/// <returns>A <see cref="System.Drawing.Image"/> containg an image of the desktop 
		/// at the specified coordinates</returns>
		internal static BitmapSource GetDesktopBitmap(Rectangle rectangle)
		{
			return GetDesktopBitmap(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
		}

		/// <summary>
		/// Retrieves an image of the specified part of your screen.
		/// </summary>
		/// <param name="x">The X coordinate of the requested area</param> 
		/// <param name="y">The Y coordinate of the requested area</param> 
		/// <param name="width">The width of the requested area</param> 
		/// <param name="height">The height of the requested area</param> 
		/// <returns>A <see cref="System.Drawing.Image"/> of the desktop at 
		/// the specified coordinates.</returns> 
		internal static BitmapSource GetDesktopBitmap(int x, int y, int width, int height)
		{
			//Create the image and graphics to capture the portion of the desktop.
			using (var destinationImage = new Bitmap(width, height)) 
			{
				using (var destinationGraphics = Graphics.FromImage(destinationImage)) 
				{
					var destinationGraphicsHandle = IntPtr.Zero;
					var windowDC = IntPtr.Zero;
					try {

						//Pointers for window handles
						destinationGraphicsHandle = destinationGraphics.GetHdc();
						windowDC = GetDC(IntPtr.Zero);

						//Get the screencapture
						var dwRop = SRCCOPY;
						BitBlt(destinationGraphicsHandle, 0, 0, width, height, windowDC, x, y, dwRop);

					} finally {
						destinationGraphics.ReleaseHdc(destinationGraphicsHandle);
						if (!windowDC.Equals(IntPtr.Zero)) {
							DeleteDC(windowDC);
						}
					}
					return Convert(destinationImage);
				}
			}
		}

		public static BitmapSource Convert(Bitmap bitmap)
		{
			var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
			var bitmapSource = BitmapSource.Create(
				bitmapData.Width, bitmapData.Height, 96, 96, PixelFormats.Bgr32, null,
				bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

			bitmap.UnlockBits(bitmapData);
			bitmapSource.Freeze(); // make it readable on any thread
			return bitmapSource;
		}

		#endregion
	}
}
