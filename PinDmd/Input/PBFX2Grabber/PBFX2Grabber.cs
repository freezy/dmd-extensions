using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PinDmd.Input.ScreenGrabber;
using Color = System.Windows.Media.Color;

namespace PinDmd.Input.PBFX2Grabber
{
	public class PBFX2Grabber : IFrameSource
	{
		public double FramesPerSecond { get; set; } = 15;
		private IntPtr _handle;

		public PBFX2Grabber()
		{
			var n = 0;
			foreach (var proc in Process.GetProcesses()) {
				if (proc.ProcessName == "Pinball FX2" && !string.IsNullOrEmpty(proc.MainWindowTitle)) {

					Console.WriteLine("{0}: {1} ({2})", proc.ProcessName, proc.MainWindowTitle, proc.Id);
					var handles = GetRootWindowsOfProcess(proc.Id);
					foreach (var handle in handles) {
						var grabbed = PrintWindow(handle);
						if (grabbed != null) {
							grabbed.Save("pbfx2-" + n + ".png");
							n++;
						}
					}
				}
			}
		}

		public IObservable<BitmapSource> GetFrames()
		{
			var bmp = new BitmapImage(new Uri("rgb-128x32.png", UriKind.Relative));
			return Observable
				.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
				.Select(x => {
					
					return bmp;
				});
		}

		[DllImport("user32.dll", SetLastError = true)]
		internal static extern bool GetWindowRect(IntPtr hWnd, out NativeCapture.RECT lpRect);

		[DllImport("user32.dll", SetLastError = true)]
		internal static extern bool PrintWindow(IntPtr hWnd, IntPtr hDC, uint nFlags);

		[DllImport("user32.dll")]
		internal static extern int GetWindowRgn(IntPtr hWnd, IntPtr hRgn);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nReghtRect, int nBottomRect);


		public static Bitmap PrintWindow(IntPtr hwnd)
		{
			NativeCapture.RECT rc;
			GetWindowRect(hwnd, out rc);

			if (rc.Right == 0) {
				return null;
			}

			Bitmap bmp = new Bitmap(rc.Right - rc.Left, rc.Bottom - rc.Top, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			Graphics gfxBmp = Graphics.FromImage(bmp);
			IntPtr hdcBitmap = gfxBmp.GetHdc();
			bool succeeded = PrintWindow(hwnd, hdcBitmap, 0);
			gfxBmp.ReleaseHdc(hdcBitmap);
			if (!succeeded) {
				gfxBmp.FillRectangle(new SolidBrush(System.Drawing.Color.Gray), new Rectangle(Point.Empty, bmp.Size));
			}
			IntPtr hRgn = CreateRectRgn(0, 0, 0, 0);
			GetWindowRgn(hwnd, hRgn);
			Region region = Region.FromHrgn(hRgn);
			if (!region.IsEmpty(gfxBmp)) {
				gfxBmp.ExcludeClip(region);
				gfxBmp.Clear(System.Drawing.Color.Transparent);
			}
			gfxBmp.Dispose();
			return bmp;
		}


		[DllImport("user32.dll")]
		public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		[DllImport("user32.Dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool EnumChildWindows(IntPtr parentHandle, Win32Callback callback, IntPtr lParam);

		public delegate bool Win32Callback(IntPtr hwnd, IntPtr lParam);

		List<IntPtr> GetRootWindowsOfProcess(int pid)
		{
			List<IntPtr> rootWindows = GetChildWindows(IntPtr.Zero);
			List<IntPtr> dsProcRootWindows = new List<IntPtr>();
			foreach (IntPtr hWnd in rootWindows) {
				uint lpdwProcessId;
				GetWindowThreadProcessId(hWnd, out lpdwProcessId);
				if (lpdwProcessId == pid)
					dsProcRootWindows.Add(hWnd);
			}
			return dsProcRootWindows;
		}

		public static List<IntPtr> GetChildWindows(IntPtr parent)
		{
			List<IntPtr> result = new List<IntPtr>();
			GCHandle listHandle = GCHandle.Alloc(result);
			try {
				Win32Callback childProc = new Win32Callback(EnumWindow);
				EnumChildWindows(parent, childProc, GCHandle.ToIntPtr(listHandle));
			} finally {
				if (listHandle.IsAllocated)
					listHandle.Free();
			}
			return result;
		}

		private static bool EnumWindow(IntPtr handle, IntPtr pointer)
		{
			GCHandle gch = GCHandle.FromIntPtr(pointer);
			List<IntPtr> list = gch.Target as List<IntPtr>;
			if (list == null) {
				throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
			}
			list.Add(handle);
			//  You can modify this to check to see if you want to cancel the operation, then return a null here
			return true;
		}
	}
}
