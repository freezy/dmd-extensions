using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PinDmd
{
	/// <summary>
	/// Class for getting images of the desktop.
	/// </summary>
	/// <threadsafety static="false" instance="false"/>
	/// <note type="caution">This class is not thread safe.</note> 
	/// <remarks>This class has been scaled back to the essentials for capturing a segment of 
	/// the desktop in order to keep Cropper as small as possible.</remarks>
	internal static class NativeMethods
	{
		#region Dll Imports

		[DllImport("user32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)]
		internal static extern IntPtr WindowFromPoint(POINT point);

		[DllImport("user32.dll")]
		internal static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)]
		internal static extern IntPtr GetDesktopWindow();

		[DllImport("user32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)]
		internal static extern IntPtr GetWindowDC(IntPtr hwnd);

		[DllImport("user32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)]
		internal static extern IntPtr GetDC(IntPtr hwnd);

		[DllImport("user32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)]
		internal static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

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

		[DllImport("user32.dll")]
		static extern int GetSystemMetrics(int smIndex);

		[DllImport("user32.dll", EntryPoint = "SendMessageA", CharSet = CharSet.Ansi, SetLastError = false)]
		internal static extern IntPtr SendMessage(IntPtr hWnd, Int32 msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MapVirtualKeyExW", ExactSpelling = true)]
		internal static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

		[DllImport("user32", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern int GetKeyNameText(uint lParam, StringBuilder lpString, int nSize);

		[DllImport("user32.dll")]
		internal static extern IntPtr GetKeyboardLayout(uint idThread);

		[DllImport("user32.dll", EntryPoint = "SendMessageA", CharSet = CharSet.Ansi, SetLastError = false)]
		internal static extern Int32 SendMessage(IntPtr hWnd, Int32 msg, Int32 wParam, IntPtr lParam);

		/// <summary>
		/// The GetCursorInfo function retrieves information about the global cursor.
		/// </summary>
		/// <param name="pci">Pointer to a CURSORINFO structure that receives the information. Note that you must set CURSORINFO.cbSize to sizeof(CURSORINFO) before calling this function.</param>
		/// <returns>
		/// If the function succeeds, the return value is nonzero.
		/// If the function fails, the return value is zero. To get extended error information, call GetLastError.
		/// </returns>
		[DllImport("user32.dll")]
		static extern bool GetCursorInfo(out CURSORINFO pci);

		/// <summary>
		/// The GetIconInfo function retrieves information about the specified icon or cursor.
		/// </summary>
		/// <param name="hIcon">
		/// Handle to the icon or cursor. To retrieve information about a standard icon or cursor, specify one of the following values.
		///     IDC_APPSTARTING     Standard arrow and small hourglass cursor.
		///     IDC_ARROW           Standard arrow cursor.
		///     IDC_CROSS           Crosshair cursor.
		///     IDC_HAND            Windows 98/Me, Windows 2000/XP: Hand cursor.
		///     IDC_HELP            Arrow and question mark cursor.
		///     IDC_IBEAM           I-beam cursor.
		///     IDC_NO              Slashed circle cursor.
		///     IDC_SIZEALL         Four-pointed arrow cursor pointing north, south, east, and west.
		///     IDC_SIZENESW        Double-pointed arrow cursor pointing northeast and southwest.
		///     IDC_SIZENS          Double-pointed arrow cursor pointing north and south.
		///     IDC_SIZENWSE        Double-pointed arrow cursor pointing northwest and southeast.
		///     IDC_SIZEWE          Double-pointed arrow cursor pointing west and east.
		///     IDC_UPARROW         Vertical arrow cursor.
		///     IDC_WAIT            Hourglass cursor.
		///     IDI_APPLICATION     Application icon.
		///     IDI_ASTERISK        Asterisk icon.
		///     IDI_EXCLAMATION     Exclamation point icon.
		///     IDI_HAND            Stop sign icon.
		///     IDI_QUESTION        Question-mark icon.
		///     IDI_WINLOGO         Windows logo icon. Windows XP: Application icon.
		/// </param>
		/// <param name="piconinfo">Pointer to an ICONINFO structure. The function fills in the structure's members.</param>
		/// <returns>
		/// If the function succeeds, the return value is nonzero and the function fills in the members of the specified ICONINFO structure.
		/// If the function fails, the return value is zero. To get extended error information, call GetLastError.
		/// </returns>
		/// <remarks>
		/// GetIconInfo creates bitmaps for the hbmMask and hbmColor members of ICONINFO. The calling application must manage these bitmaps and delete them when they are no longer necessary.
		/// </remarks>
		[DllImport("user32.dll")]
		private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

		/// <summary>
		/// The CopyIcon function copies the specified icon from another module to the current module.
		/// </summary>
		/// <param name="hIcon"> Handle to the icon to be copied.</param>
		/// <returns>
		/// If the function succeeds, the return value is a handle to the duplicate icon.
		/// If the function fails, the return value is NULL. To get extended error information, call GetLastError.
		/// </returns>
		/// <remarks>
		/// The CopyIcon function enables an application or DLL to get its own handle to an icon owned by another module. If the other module is freed, the application icon will still be able to use the icon.
		/// Before closing, an application must call the DestroyIcon function to free any system resources associated with the icon.
		/// </remarks>
		[DllImport("user32.dll")]
		static extern IntPtr CopyIcon(IntPtr hIcon);

		/// <summary>
		/// The DeleteObject function deletes a logical pen, brush, font, bitmap, region, or palette, freeing all system resources associated with the object. After the object is deleted, the specified handle is no longer valid.
		/// </summary>
		/// <param name="hDc">A handle to a logical pen, brush, font, bitmap, region, or palette.</param>
		/// <returns>
		/// If the function succeeds, the return value is nonzero.
		/// If the specified handle is not valid or is currently selected into a DC, the return value is zero.
		/// </returns>
		/// <remarks>
		/// Do not delete a drawing object (pen or brush) while it is still selected into a DC.
		/// When a pattern brush is deleted, the bitmap associated with the brush is not deleted. The bitmap must be deleted independently.
		/// </remarks>
		[DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
		private static extern IntPtr DeleteObject(IntPtr hDc);

		/// <summary>
		/// Destroys an icon and frees any memory the icon occupied. 
		/// </summary>
		/// <param name="hIcon">Handle to the icon to be destroyed. The icon must not be in use.</param>
		/// <returns>
		/// If the function succeeds, the return value is nonzero.
		/// If the function fails, the return value is zero. To get extended error information, call GetLastError.
		/// </returns>
		/// <remarks>
		/// It is only necessary to call DestroyIcon for icons and cursors created with the following functions: CreateIconFromResourceEx (if called without the LR_SHARED flag), CreateIconIndirect, and CopyIcon. Do not use this function to destroy a shared icon. A shared icon is valid as long as the module from which it was loaded remains in memory. The following functions obtain a shared icon.
		///     LoadIcon
		///     LoadImage (if you use the LR_SHARED flag)
		///     CopyImage (if you use the LR_COPYRETURNORG flag and the hImage parameter is a shared icon)
		///     CreateIconFromResource
		///     CreateIconFromResourceEx (if you use the LR_SHARED flag)
		/// </remarks>
		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool DestroyIcon(IntPtr hIcon);

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

		public enum InvalidHotKeyModifiers
		{
			HKCOMB_NONE = 1,
			HKCOMB_S = 2,
			HKCOMB_C = 4,
			HKCOMB_A = 8,
			HKCOMB_SC = 16,
			HKCOMB_SA = 32,
			HKCOMB_CA = 64,
			HKCOMB_SCA = 128
		}

		#region Structures

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;


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

		/// <summary>
		/// Contains global cursor information.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		struct CURSORINFO
		{
			/// <summary>
			/// Specifies the size, in bytes, of the structure. The caller must set this to Marshal.SizeOf(typeof(CURSORINFO)).
			/// </summary>
			public Int32 cbSize;
			/// <summary>
			/// Specifies the cursor state. This parameter can be one of the following values.
			///     0               The cursor is hidden.
			///     CURSOR_SHOWING  The cursor is showing.
			/// </summary>
			public Int32 flags;
			/// <summary>
			/// Handle to the cursor.
			/// </summary>
			public IntPtr hCursor;
			/// <summary>
			/// A POINT structure that receives the screen coordinates of the cursor.
			/// </summary>
			public POINT ptScreenPos;
		}

		/// <summary>
		/// Contains information about an icon or a cursor.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		struct ICONINFO
		{
			/// <summary>
			/// Specifies whether this structure defines an icon or a cursor. A value of TRUE specifies an icon; FALSE specifies a cursor.
			/// </summary>
			public bool fIcon;
			/// <summary>
			/// Specifies the x-coordinate of a cursor's hot spot. If this structure defines an icon, the hot spot is always in the center of the icon, and this member is ignored.
			/// </summary>
			public Int32 xHotspot;
			/// <summary>
			/// Specifies the y-coordinate of the cursor's hot spot. If this structure defines an icon, the hot spot is always in the center of the icon, and this member is ignored.
			/// </summary>
			public Int32 yHotspot;
			/// <summary>
			/// Specifies the icon bitmask bitmap. If this structure defines a black and white icon, this bitmask is formatted so that the upper half is the icon AND bitmask and the lower half is the icon XOR bitmask. Under this condition, the height should be an even multiple of two. If this structure defines a color icon, this mask only defines the AND bitmask of the icon.
			/// </summary>
			public IntPtr hbmMask;
			/// <summary>
			/// Handle to the icon color bitmap. This member can be optional if this structure defines a black and white icon. The AND bitmask of hbmMask is applied with the SRCAND flag to the destination; subsequently, the color bitmap is applied (using XOR) to the destination by using the SRCINVERT flag.
			/// </summary>
			public IntPtr hbmColor;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Gets a segment of the desktop as an image.
		/// </summary>
		/// <returns>A <see cref="System.Drawing.Image"/> containg an image of the full desktop.</returns>
		internal static Image GetDesktopBitmap()
		{
			return GetDesktopBitmap(FindWindow(null, "Program Manager"));
		}

		/// <summary>
		/// Gets a segment of the desktop as an image.
		/// </summary>
		/// <returns>A <see cref="System.Drawing.Image"/> containg an image of the full desktop.</returns>
		internal static Image GetDesktopBitmap(IntPtr hWnd)
		{
			return GetDesktopBitmap(hWnd, false, Color.Empty);
		}

		/// <summary>
		/// Gets a segment of the desktop as an image.
		/// </summary>
		/// <returns>A <see cref="System.Drawing.Image"/> containg an image of the full desktop.</returns>
		internal static Image GetDesktopBitmap(IntPtr hWnd, bool colorNonFormArea, Color backgroundColor)
		{
			Image capture = null;

			try {
				RECT rect = new RECT();
				GetWindowRect(hWnd, ref rect);
				capture = GetDesktopBitmap(rect.ToRectangle());

				if (colorNonFormArea)
					return ColorNonRegionFormArea(hWnd, capture, backgroundColor);
				else
					return capture;
			} finally {
				if (capture != null && colorNonFormArea)
					capture.Dispose();
			}
		}

		/// <summary>
		/// Gets a segment of the desktop as an image.
		/// </summary>
		/// <param name="rectangle">The rectangular area to capture.</param>
		/// <returns>A <see cref="System.Drawing.Image"/> containg an image of the desktop 
		/// at the specified coordinates</returns>
		internal static Image GetDesktopBitmap(Rectangle rectangle)
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
		internal static Image GetDesktopBitmap(int x, int y, int width, int height)
		{
			//Create the image and graphics to capture the portion of the desktop.
			Image destinationImage = new Bitmap(width, height);
			Graphics destinationGraphics = Graphics.FromImage(destinationImage);

			IntPtr destinationGraphicsHandle = IntPtr.Zero;

			try {
				//Pointers for window handles
				destinationGraphicsHandle = destinationGraphics.GetHdc();
				IntPtr windowDC = GetDC(IntPtr.Zero);

				//Get the screencapture
				int dwRop = SRCCOPY;

				BitBlt(destinationGraphicsHandle, 0, 0, width, height, windowDC, x, y, dwRop);
			} finally {
				destinationGraphics.ReleaseHdc(destinationGraphicsHandle);
			}

			// Don't forget to dispose this image
			return destinationImage;
		}

		private static Region GetRegionByHWnd(IntPtr hWnd)
		{
			IntPtr windowRegion = CreateRectRgn(0, 0, 0, 0);
			GetWindowRgn(hWnd, windowRegion);
			return Region.FromHrgn(windowRegion);
		}

		private static Bitmap ColorNonRegionFormArea(IntPtr hWnd, Image capture, Color color)
		{
			Bitmap finalCapture;

			using (Region region = GetRegionByHWnd(hWnd))
			using (Graphics drawGraphics = Graphics.FromImage(capture))
			using (SolidBrush brush = new SolidBrush(color)) {
				RectangleF bounds = region.GetBounds(drawGraphics);
				if (bounds == RectangleF.Empty) {
					GraphicsUnit unit = GraphicsUnit.Pixel;
					bounds = capture.GetBounds(ref unit);

					if ((GetWindowLongA(hWnd, GWL_STYLE) & TARGETWINDOW) == TARGETWINDOW) {
						IntPtr windowRegion = CreateRoundRectRgn(0, 0, (int)bounds.Width + 1, (int)bounds.Height + 1, 9, 9);
						Region r = Region.FromHrgn(windowRegion);

						r.Complement(bounds);
						drawGraphics.FillRegion(brush, r);
					}
				} else {
					region.Complement(bounds);
					drawGraphics.FillRegion(brush, region);
				}

				finalCapture = new Bitmap((int)bounds.Width, (int)bounds.Height);
				using (Graphics finalGraphics = Graphics.FromImage(finalCapture)) {
					finalGraphics.SmoothingMode = SmoothingMode.AntiAlias;
					finalGraphics.DrawImage(capture, new RectangleF(new PointF(0, 0), finalCapture.Size), bounds, GraphicsUnit.Pixel);
				}
			}
			return finalCapture;
		}

		#endregion
	}
}
