using System;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LibDmd.Output.VirtualDmd;
using NLog;

namespace DmdExt.Common
{
	/// <summary>
	/// A borderless virtual DMD that resizes with the same aspect ratio (if not disabled)
	/// </summary>
	public partial class VirtualDmd : Window, DmdWindow
	{
		/// <summary>
		/// If true, the DMD stays on top of all other application windows.
		/// </summary>
		public bool AlwaysOnTop { get; set; }

		public readonly BehaviorSubject<DmdPosition> PositionChanged;

		public bool IgnoreAspectRatio
		{
			get { return _ignoreAr; }
			set {
				_ignoreAr = value;
				if (Dmd != null) {
					Dmd.IgnoreAspectRatio = value;
				}
			}
		}

		public Brush GripColor { get; set; } = Brushes.White;

		private bool _ignoreAr;
		private double _aspectRatio;
		private bool? _adjustingHeight;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmd()
		{
			DataContext = this;
			InitializeComponent();
			SourceInitialized += Window_SourceInitialized;
			MouseDown += Window_MouseDown;
			Deactivated += Window_Deactivated;
			LocationChanged += LocationChanged_Event;
			SizeChanged += LocationChanged_Event;

			Dmd.Host = this;
			PositionChanged = new BehaviorSubject<DmdPosition>(new DmdPosition(Left, Top, Width, Height));
		}

		public void SetDimensions(int width, int height)
		{
			Logger.Info("Resizing Virtual DMD to {0}x{1}.", width, height);
			_aspectRatio = (double)width / height;
			Height = Width / _aspectRatio;
		}

		private void LocationChanged_Event(object sender, EventArgs e)
		{
			PositionChanged.OnNext(new DmdPosition(Left, Top, Width, Height));
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
			if (_ignoreAr) {
				return IntPtr.Zero;
			}

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

	public class DmdPosition
	{
		public double Left { get; set; }
		public double Top { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }

		public DmdPosition(double left, double top, double width, double height)
		{
			Left = left;
			Top = top;
			Width = width;
			Height = height;
		}

		public override string ToString()
		{
			return $"{Width}x{Height}@{Left}/{Top}";
		}
	}
}
