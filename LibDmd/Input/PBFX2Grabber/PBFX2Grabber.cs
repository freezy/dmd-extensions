using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NLog;
using LibDmd.Input.ScreenGrabber;

namespace LibDmd.Input.PBFX2Grabber
{
	/// <summary>
	/// Polls for the Pinball FX2 process, searches for the DMD display grabs pixels off
	/// the display, even if it's hidden (e.g. off the screen or behind the playfield).
	/// </summary>
	/// <remarks>
	/// Can be launched any time. Will wait with sending frames until Pinball FX2 is
	/// launched and stop sending when it exits.
	/// </remarks>
	public class PBFX2Grabber : IFrameSource
	{
		/// <summary>
		/// Wait time between polls for the Pinball FX2 process. Stops polling as soon
		/// as the process is found. 
		/// 
		/// Can be set quite high, just about as long as it takes for Pinball FX2 to launch
		/// and load a game.
		/// </summary>
		public TimeSpan PollForProcessDelay { get; set; } = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Frequency with which frames are pulled off the display.
		/// </summary>
		public double FramesPerSecond { get; set; } = 15;

		public double CropLeft { get; set; } = 1.5;
		public double CropRight { get; set; } = 1;

		private IntPtr _handle;
		private IDisposable _poller;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private void PollForHandle() {
			_handle = FindDmdHandle();
			if (_handle == IntPtr.Zero) {
				Logger.Info("Pinball FX2 not running, waiting...");
				_poller = Observable.Interval(PollForProcessDelay).Subscribe(x => {
					_handle = FindDmdHandle();
					if (_handle != IntPtr.Zero) {
						Logger.Info("Pinball FX2 running, starting to capture.");
						_poller.Dispose();
					}
				});
			}
		}

		public IObservable<BitmapSource> GetFrames()
		{
			PollForHandle();
			return Observable
				.Interval(TimeSpan.FromMilliseconds(1000/FramesPerSecond))
				.Where(x => _handle != IntPtr.Zero)
				.Select(x => CaptureWindow())
				.Where(bmp => bmp != null);
		}

		public BitmapSource CaptureWindow()
		{
			NativeCapture.RECT rc;
			GetWindowRect(_handle, out rc);

			// rect contains 0 values if handler not available
			if (rc.Width == 0 || rc.Height == 0) {
				_handle = IntPtr.Zero;
				PollForHandle();
				return null;
			}

			using (var bmp = new Bitmap(rc.Width, rc.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
			{
				using (var gfxBmp = Graphics.FromImage(bmp))
				{
					var hdcBitmap = gfxBmp.GetHdc();
					try {
						var succeeded = PrintWindow(_handle, hdcBitmap, 0);
						if (!succeeded) {
							Logger.Error("Could not retrieve image data from handle {0}", _handle);
							return null;
						}
					} finally {
						gfxBmp.ReleaseHdc(hdcBitmap);
					}
					return Convert(bmp);
				}
			}
		}

		private static IntPtr FindDmdHandle()
		{
			foreach (var proc in Process.GetProcessesByName("Pinball FX2")) {
				var handles = GetRootWindowsOfProcess(proc.Id);
				foreach (var handle in handles) {
					NativeCapture.RECT rc;
					GetWindowRect(handle, out rc);
					if (rc.Width == 0 || rc.Height == 0) {
						continue;
					}
					var ar = rc.Width / rc.Height;
					if (ar >= 3 && ar < 4.2) {
						return handle;
					}
				}
				Logger.Warn("Pinball FX2 process found (pid {0}) but DMD not. No game running?", proc.Id);
			}
			return IntPtr.Zero;
		}

		private static IEnumerable<IntPtr> GetRootWindowsOfProcess(int pid)
		{
			var rootWindows = GetChildWindows(IntPtr.Zero);
			var dsProcRootWindows = new List<IntPtr>();
			foreach (var hWnd in rootWindows) {
				uint lpdwProcessId;
				GetWindowThreadProcessId(hWnd, out lpdwProcessId);
				if (lpdwProcessId == pid) {
					dsProcRootWindows.Add(hWnd);
				}
			}
			return dsProcRootWindows;
		}

		private static IEnumerable<IntPtr> GetChildWindows(IntPtr parent)
		{
			var result = new List<IntPtr>();
			var listHandle = GCHandle.Alloc(result);
			try {
				Win32Callback childProc = EnumWindow;
				EnumChildWindows(parent, childProc, GCHandle.ToIntPtr(listHandle));
			} finally {
				if (listHandle.IsAllocated) {
					listHandle.Free();
				}
			}
			return result;
		}

		private static bool EnumWindow(IntPtr handle, IntPtr pointer)
		{
			var gch = GCHandle.FromIntPtr(pointer);
			var list = gch.Target as List<IntPtr>;
			if (list == null) {
				throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
			}
			list.Add(handle);
			// You can modify this to check to see if you want to cancel the operation, then return a null here
			return true;
		}

		public BitmapSource Convert(Bitmap bitmap)
		{
			var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
			var bitmapSource = BitmapSource.Create(
				bitmapData.Width, bitmapData.Height, 96, 96, PixelFormats.Bgr32, null,
				bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

			bitmap.UnlockBits(bitmapData);
			bitmapSource.Freeze(); // make it readable on any thread

			// crop border (1 dot)
			var dotWidth = (bitmapSource.PixelWidth / 130);
			var dotHeight = (bitmapSource.PixelHeight / 34);
			var cropLeft = Math.Max(0, CropLeft);
			var cropRight = Math.Max(0, CropRight);
			const double cropY = 1;
			var img = new CroppedBitmap(bitmapSource, new Int32Rect(
				(int)Math.Round(dotWidth * cropLeft), 
				(int)Math.Round(dotHeight * cropY), 
				(int)Math.Round(bitmapSource.PixelWidth - dotWidth * (cropLeft + cropRight)), 
				(int)Math.Round(bitmapSource.PixelHeight - dotHeight * 2.5d)));
			img.Freeze();

			return img;
		}

		#region Dll Imports

		public delegate bool Win32Callback(IntPtr hwnd, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		internal static extern bool GetWindowRect(IntPtr hWnd, out NativeCapture.RECT lpRect);

		[DllImport("user32.dll", SetLastError = true)]
		internal static extern bool PrintWindow(IntPtr hWnd, IntPtr hDC, uint nFlags);

		[DllImport("user32.dll")]
		internal static extern int GetWindowRgn(IntPtr hWnd, IntPtr hRgn);

		[DllImport("gdi32.dll")]
		internal static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nReghtRect, int nBottomRect);
		[DllImport("user32.dll")]
		public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool EnumChildWindows(IntPtr parentHandle, Win32Callback callback, IntPtr lParam);

		#endregion

		private static void Dump(BitmapSource bmp, string filePath)
		{
			using (var fileStream = new FileStream(filePath, FileMode.Create)) {
				BitmapEncoder encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(bmp));
				encoder.Save(fileStream);
			}
		}
	}
}
