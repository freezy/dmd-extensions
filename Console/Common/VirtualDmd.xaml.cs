using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace App
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio.
	/// </summary>
	public partial class VirtualDmd : Window
	{
		/// <summary>
		/// If true, the DMD stays on top of all other application windows.
		/// </summary>
		public bool AlwaysOnTop { get; set; }

		private double _aspectRatio;
		private bool? _adjustingHeight;

		public VirtualDmd()
		{
			InitializeComponent();
			SourceInitialized += Window_SourceInitialized;
			MouseDown += Window_MouseDown;
			Deactivated += Window_Deactivated;
		}

		public static Point GetMousePosition() // mouse position relative to screen
		{
			var w32Mouse = new Win32Point();
			GetCursorPos(ref w32Mouse);
			return new Point(w32Mouse.X, w32Mouse.Y);
		}

		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left) {
				DragMove();
			}
		}

		private void Window_Deactivated(object sender, EventArgs e)
		{
			if (AlwaysOnTop) {
				var window = (Window)sender;
				window.Topmost = true;
			}
		}


		private void Window_SourceInitialized(object sender, EventArgs ea)
		{
			var hwndSource = (HwndSource)PresentationSource.FromVisual((Window)sender);
			hwndSource?.AddHook(DragHook);

			_aspectRatio = Width / Height;
		}

		private IntPtr DragHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			switch ((WM)msg) {
				case WM.WindowPosChanging: {
						var pos = (WindowPos)Marshal.PtrToStructure(lParam, typeof(WindowPos));

						if ((pos.flags & (int)SWP.NoMove) != 0) {
							return IntPtr.Zero;
						}

						var wnd = (Window)HwndSource.FromHwnd(hwnd)?.RootVisual;
						if (wnd == null) {
							return IntPtr.Zero;
						}

						// determine what dimension is changed by detecting the mouse position relative to the 
						// window bounds. if gripped in the corner, either will work.
						if (!_adjustingHeight.HasValue) {
							var p = GetMousePosition();

							var diffWidth = Math.Min(Math.Abs(p.X - pos.x), Math.Abs(p.X - pos.x - pos.cx));
							var diffHeight = Math.Min(Math.Abs(p.Y - pos.y), Math.Abs(p.Y - pos.y - pos.cy));

							_adjustingHeight = diffHeight > diffWidth;
						}

						if (_adjustingHeight.Value) {
							pos.cy = (int)(pos.cx / _aspectRatio); // adjusting height to width change

						} else {
							pos.cx = (int)(pos.cy * _aspectRatio); // adjusting width to heigth change
						}
						Marshal.StructureToPtr(pos, lParam, true);
						handled = true;
					}
					break;

				case WM.ExitSizeMove:
					_adjustingHeight = null; // reset adjustment dimension and detect again next time window is resized
					break;
			}
			return IntPtr.Zero;
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct WindowPos
		{
			public IntPtr hwnd;
			public IntPtr hwndInsertAfter;
			public int x;
			public int y;
			public int cx;
			public int cy;
			public int flags;
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct Win32Point
		{
			public int X;
			public int Y;
		};

		internal enum SWP
		{
			NoMove = 0x0002
		}

		internal enum WM
		{
			WindowPosChanging = 0x0046,
			ExitSizeMove = 0x0232,
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool GetCursorPos(ref Win32Point pt);
	}
}
