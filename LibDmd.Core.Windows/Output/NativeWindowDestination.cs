using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Output.Virtual.Dmd;
using NLog;

namespace LibDmd.Output.NativeWindow
{
	public sealed class NativeWindowDestination : IGray2Destination, IGray4Destination, IGray8Destination, IRgb24Destination, IRgb565Destination, IFixedSizeDestination
	{
		private const int Scale = 4;
		private readonly object _frameLock = new object();
		private readonly ManualResetEventSlim _windowReady = new ManualResetEventSlim();
		private readonly Thread _windowThread;
		private readonly Dimensions _size;
		private readonly byte[] _rgba;
		private readonly byte[] _renderBuffer;
		private int _windowLeft;
		private int _windowTop;
		private int _windowWidth;
		private int _windowHeight;
		private bool _stayOnTop;
		private VirtualDmdRenderStyle _renderStyle;
		private Color _color = Color.FromRgb(255, 88, 0);
		private IntPtr _hwnd;
		private NativeOpenGlRenderer _renderer;
		private bool _disposed;
		private bool _isMovingOrSizing;
		private int _renderPending;
		private int _configurePending;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const int ResizeGripSize = 18;

		public NativeWindowDestination(int width, int height)
			: this(width, height, 100, 100, width * Scale, height * Scale, false, VirtualDmdRenderStyle.Default)
		{
		}

		public NativeWindowDestination(int width, int height, int windowLeft, int windowTop, int windowWidth, int windowHeight, bool stayOnTop)
			: this(width, height, windowLeft, windowTop, windowWidth, windowHeight, stayOnTop, VirtualDmdRenderStyle.Default)
		{
		}

		public NativeWindowDestination(int width, int height, int windowLeft, int windowTop, int windowWidth, int windowHeight, bool stayOnTop, VirtualDmdRenderStyle renderStyle)
		{
			_size = new Dimensions(width, height);
			_rgba = new byte[_size.Surface * 4];
			_renderBuffer = new byte[_rgba.Length];
			_windowLeft = windowLeft;
			_windowTop = windowTop;
			_windowWidth = windowWidth > 0 ? windowWidth : width * Scale;
			_windowHeight = windowHeight > 0 ? windowHeight : height * Scale;
			_stayOnTop = stayOnTop;
			_renderStyle = renderStyle ?? VirtualDmdRenderStyle.Default;
			_windowThread = new Thread(WindowThreadMain) {
				IsBackground = true,
				Name = "LibDmd Native Window"
			};
			_windowThread.SetApartmentState(ApartmentState.STA);
			_windowThread.Start();
			_windowReady.Wait(TimeSpan.FromSeconds(2));
		}

		public string Name => "Native DMD Window";
		public bool IsAvailable => _hwnd != IntPtr.Zero && !_disposed;
		public bool NeedsDuplicateFrames => false;
		public bool NeedsIdentificationFrames => false;
		public Dimensions FixedSize => _size;
		public bool DmdAllowHdScaling => false;
		public int WindowLeft => _windowLeft;
		public int WindowTop => _windowTop;
		public int WindowWidth => _windowWidth;
		public int WindowHeight => _windowHeight;
		public bool WindowStayOnTop => _stayOnTop;
		public bool IsMovingOrSizing => _isMovingOrSizing;

		public void RenderGray2(DmdFrame frame) => RenderGray(frame, 3);
		public void RenderGray4(DmdFrame frame) => RenderGray(frame, 15);
		public void RenderGray8(DmdFrame frame) => RenderGray(frame, 255);

		public void RenderRgb565(DmdFrame frame)
		{
			if (_disposed || frame?.Data == null) {
				return;
			}

			lock (_frameLock) {
				for (var i = 0; i < _size.Surface; i++) {
					var value = frame.Data[i * 2] | (frame.Data[i * 2 + 1] << 8);
					var r = (byte)(((value >> 11) & 0x1f) * 255 / 31);
					var g = (byte)(((value >> 5) & 0x3f) * 255 / 63);
					var b = (byte)((value & 0x1f) * 255 / 31);
					WriteRgba(i, r, g, b);
				}
			}
			RequestPaint();
		}

		public void RenderRgb24(DmdFrame frame)
		{
			if (_disposed || frame?.Data == null) {
				return;
			}

			lock (_frameLock) {
				for (var i = 0; i < _size.Surface; i++) {
					WriteRgba(i, frame.Data[i * 3], frame.Data[i * 3 + 1], frame.Data[i * 3 + 2]);
				}
			}
			RequestPaint();
		}

		public void SetColor(Color color)
		{
			_color = color;
		}

		public void SetPalette(Color[] colors)
		{
			if (colors != null && colors.Length > 0) {
				_color = colors[colors.Length - 1];
			}
		}

		public void ClearColor()
		{
			_color = Color.FromRgb(255, 88, 0);
		}

		public void ClearPalette()
		{
		}

		public void ConfigureWindow(int left, int top, int width, int height, bool stayOnTop)
		{
			_windowLeft = left;
			_windowTop = top;
			_windowWidth = width > 0 ? width : _size.Width * Scale;
			_windowHeight = height > 0 ? height : _size.Height * Scale;
			_stayOnTop = stayOnTop;

			var hwnd = _hwnd;
			if (hwnd != IntPtr.Zero && Interlocked.Exchange(ref _configurePending, 1) == 0) {
				PostMessage(hwnd, WM_CONFIGURE_WINDOW, IntPtr.Zero, IntPtr.Zero);
			}
		}

		public void ConfigureRenderStyle(VirtualDmdRenderStyle renderStyle)
		{
			_renderStyle = renderStyle ?? VirtualDmdRenderStyle.Default;
			_renderer?.SetStyle(_renderStyle);
			RequestPaint();
		}

		public void ClearDisplay()
		{
			lock (_frameLock) {
				Array.Clear(_rgba, 0, _rgba.Length);
			}
			RequestPaint();
		}

		public void Dispose()
		{
			_disposed = true;
			if (_hwnd != IntPtr.Zero) {
				PostMessage(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
			}
			_windowReady.Dispose();
		}

		private void RenderGray(DmdFrame frame, int maxValue)
		{
			if (_disposed || frame?.Data == null) {
				return;
			}

			lock (_frameLock) {
				for (var i = 0; i < _size.Surface; i++) {
					var intensity = frame.Data[i] / (float)maxValue;
					WriteRgba(
						i,
						(byte)(_color.R * intensity),
						(byte)(_color.G * intensity),
						(byte)(_color.B * intensity));
				}
			}
			RequestPaint();
		}

		private void WriteRgba(int pixel, byte r, byte g, byte b)
		{
			var offset = pixel * 4;
			_rgba[offset] = r;
			_rgba[offset + 1] = g;
			_rgba[offset + 2] = b;
			_rgba[offset + 3] = 255;
		}

		private void RequestPaint()
		{
			var hwnd = _hwnd;
			if (hwnd != IntPtr.Zero && !_isMovingOrSizing && Interlocked.Exchange(ref _renderPending, 1) == 0) {
				PostMessage(hwnd, WM_RENDER, IntPtr.Zero, IntPtr.Zero);
			}
		}

		private void WindowThreadMain()
		{
			NativeWindowClass.Register();
			var style = WS_POPUP | WS_THICKFRAME | WS_VISIBLE;
			_hwnd = CreateWindowEx(
				_stayOnTop ? WS_EX_TOPMOST : 0,
				NativeWindowClass.ClassName,
				"VPE DMD",
				style,
				_windowLeft,
				_windowTop,
				_windowWidth,
				_windowHeight,
				IntPtr.Zero,
				IntPtr.Zero,
				IntPtr.Zero,
				IntPtr.Zero);

			if (_hwnd == IntPtr.Zero) {
				Logger.Error($"[DMD] Could not create native DMD window. Win32 error: {Marshal.GetLastWin32Error()}.");
				_windowReady.Set();
				return;
			}

			ReadWindowLayout();
			_renderer = new NativeOpenGlRenderer(_hwnd, _size, _renderStyle);
			NativeWindowClass.SetDestination(_hwnd, this);
			_windowReady.Set();
			RequestPaint();

			while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0) {
				TranslateMessage(ref message);
				DispatchMessage(ref message);
			}

			_renderer?.Dispose();
			_renderer = null;
			NativeWindowClass.RemoveDestination(_hwnd);
			_hwnd = IntPtr.Zero;
		}

		private void Paint(IntPtr hwnd)
		{
			var hdc = BeginPaint(hwnd, out var paint);
			try {
				RenderOpenGl();
			} finally {
				EndPaint(hwnd, ref paint);
			}
		}

		private void RenderOpenGl()
		{
			var renderer = _renderer;
			if (renderer == null) {
				return;
			}

			lock (_frameLock) {
				Buffer.BlockCopy(_rgba, 0, _renderBuffer, 0, _rgba.Length);
			}

			renderer.Render(_renderBuffer);
		}

		private void ReadWindowLayout()
		{
			var hwnd = _hwnd;
			if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out var client)) {
				return;
			}

			var clientOrigin = new POINT();
			if (!ClientToScreen(hwnd, ref clientOrigin)) {
				return;
			}

			_windowLeft = clientOrigin.X;
			_windowTop = clientOrigin.Y;
			_windowWidth = Math.Max(1, client.Right - client.Left);
			_windowHeight = Math.Max(1, client.Bottom - client.Top);
		}

		private bool IsResizeGrip(IntPtr lParam)
		{
			var hwnd = _hwnd;
			if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out var client)) {
				return false;
			}

			var point = new POINT {
				X = GetSignedLoWord(lParam),
				Y = GetSignedHiWord(lParam)
			};
			if (!ScreenToClient(hwnd, ref point)) {
				return false;
			}

			return point.X >= client.Right - ResizeGripSize && point.Y >= client.Bottom - ResizeGripSize;
		}

		private void ApplyWindowConfiguration()
		{
			var hwnd = _hwnd;
			if (hwnd == IntPtr.Zero) {
				return;
			}

			SetWindowPos(
				hwnd,
				_stayOnTop ? HWND_TOPMOST : HWND_NOTOPMOST,
				_windowLeft,
				_windowTop,
				_windowWidth,
				_windowHeight,
				SWP_NOACTIVATE | SWP_SHOWWINDOW);
			RequestPaint();
		}

		private static short GetSignedLoWord(IntPtr value)
		{
			return unchecked((short)((long)value & 0xffff));
		}

		private static short GetSignedHiWord(IntPtr value)
		{
			return unchecked((short)(((long)value >> 16) & 0xffff));
		}

		private sealed class NativeWindowClass
		{
			public static readonly string ClassName = "LibDmdNativeWindow_" + Guid.NewGuid().ToString("N");
			private static readonly object SyncRoot = new object();
			private static readonly Dictionary<IntPtr, NativeWindowDestination> Destinations = new Dictionary<IntPtr, NativeWindowDestination>();
			private static readonly WndProc WindowProcedure = WndProc;
			private static bool _registered;

			public static void Register()
			{
				lock (SyncRoot) {
					if (_registered) {
						return;
					}

					var wndClass = new WNDCLASSEX {
						cbSize = Marshal.SizeOf<WNDCLASSEX>(),
						style = CS_HREDRAW | CS_VREDRAW,
						lpfnWndProc = WindowProcedure,
						hInstance = IntPtr.Zero,
						hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW),
						hbrBackground = IntPtr.Zero,
						lpszClassName = ClassName
					};

					var atom = RegisterClassEx(ref wndClass);
					if (atom == 0) {
						throw new InvalidOperationException($"Could not register native DMD window class \"{ClassName}\". Win32 error: {Marshal.GetLastWin32Error()}.");
					}

					_registered = true;
				}
			}

			public static void SetDestination(IntPtr hwnd, NativeWindowDestination destination)
			{
				lock (SyncRoot) {
					Destinations[hwnd] = destination;
				}
			}

			public static void RemoveDestination(IntPtr hwnd)
			{
				lock (SyncRoot) {
					Destinations.Remove(hwnd);
				}
			}

			private static IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
			{
				switch (message) {
					case WM_PAINT:
						if (TryGetDestination(hwnd, out var paintDestination)) {
							Interlocked.Exchange(ref paintDestination._renderPending, 0);
							paintDestination.Paint(hwnd);
							return IntPtr.Zero;
						}
						break;
					case WM_ERASEBKGND:
						return new IntPtr(1);
					case WM_NCCALCSIZE:
						return IntPtr.Zero;
					case WM_NCHITTEST:
						if (TryGetDestination(hwnd, out var hitTestDestination)) {
							return new IntPtr(hitTestDestination.IsResizeGrip(lParam) ? HTBOTTOMRIGHT : HTCAPTION);
						}
						break;
					case WM_ENTERSIZEMOVE:
						if (TryGetDestination(hwnd, out var enterDestination)) {
							enterDestination._isMovingOrSizing = true;
							Interlocked.Exchange(ref enterDestination._renderPending, 0);
							return IntPtr.Zero;
						}
						break;
					case WM_EXITSIZEMOVE:
						if (TryGetDestination(hwnd, out var exitDestination)) {
							exitDestination._isMovingOrSizing = false;
							exitDestination.ReadWindowLayout();
							exitDestination.RequestPaint();
							return IntPtr.Zero;
						}
						break;
					case WM_MOVE:
					case WM_SIZE:
						if (TryGetDestination(hwnd, out var sizeDestination)) {
							sizeDestination.ReadWindowLayout();
							if (!sizeDestination._isMovingOrSizing) {
								sizeDestination.RequestPaint();
							}
							return IntPtr.Zero;
						}
						break;
					case WM_CONFIGURE_WINDOW:
						if (TryGetDestination(hwnd, out var configureDestination)) {
							Interlocked.Exchange(ref configureDestination._configurePending, 0);
							configureDestination.ApplyWindowConfiguration();
							return IntPtr.Zero;
						}
						break;
					case WM_SHOWWINDOW:
					case WM_RENDER:
						if (TryGetDestination(hwnd, out var renderDestination)) {
							Interlocked.Exchange(ref renderDestination._renderPending, 0);
							if (!renderDestination._isMovingOrSizing) {
								renderDestination.RenderOpenGl();
							}
							return IntPtr.Zero;
						}
						break;
					case WM_CLOSE:
						DestroyWindow(hwnd);
						return IntPtr.Zero;
					case WM_DESTROY:
						PostQuitMessage(0);
						return IntPtr.Zero;
				}

				return DefWindowProc(hwnd, message, wParam, lParam);
			}

			private static bool TryGetDestination(IntPtr hwnd, out NativeWindowDestination destination)
			{
				lock (SyncRoot) {
					return Destinations.TryGetValue(hwnd, out destination);
				}
			}
		}

		private const int CW_USEDEFAULT = unchecked((int)0x80000000);
		private const int CS_HREDRAW = 0x0002;
		private const int CS_VREDRAW = 0x0001;
		private const int HTBOTTOMRIGHT = 17;
		private const int HTCAPTION = 2;
		private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
		private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
		private const int IDC_ARROW = 32512;
		private const int SWP_NOACTIVATE = 0x0010;
		private const int SWP_SHOWWINDOW = 0x0040;
		private const int WM_CLOSE = 0x0010;
		private const int WM_CONFIGURE_WINDOW = 0x8002;
		private const int WM_DESTROY = 0x0002;
		private const int WM_ENTERSIZEMOVE = 0x0231;
		private const int WM_ERASEBKGND = 0x0014;
		private const int WM_EXITSIZEMOVE = 0x0232;
		private const int WM_MOVE = 0x0003;
		private const int WM_NCCALCSIZE = 0x0083;
		private const int WM_NCHITTEST = 0x0084;
		private const int WM_PAINT = 0x000F;
		private const int WM_RENDER = 0x8001;
		private const int WM_SHOWWINDOW = 0x0018;
		private const int WM_SIZE = 0x0005;
		private const int WS_EX_TOPMOST = 0x00000008;
		private const int WS_POPUP = unchecked((int)0x80000000);
		private const int WS_THICKFRAME = 0x00040000;
		private const int WS_VISIBLE = 0x10000000;

		private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

		[StructLayout(LayoutKind.Sequential)]
		private struct WNDCLASSEX
		{
			public int cbSize;
			public int style;
			public WndProc lpfnWndProc;
			public int cbClsExtra;
			public int cbWndExtra;
			public IntPtr hInstance;
			public IntPtr hIcon;
			public IntPtr hCursor;
			public IntPtr hbrBackground;
			public string lpszMenuName;
			public string lpszClassName;
			public IntPtr hIconSm;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MSG
		{
			public IntPtr hwnd;
			public uint message;
			public IntPtr wParam;
			public IntPtr lParam;
			public uint time;
			public int ptX;
			public int ptY;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int X;
			public int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct PAINTSTRUCT
		{
			public IntPtr hdc;
			public bool fErase;
			public RECT rcPaint;
			public bool fRestore;
			public bool fIncUpdate;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
			public byte[] rgbReserved;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[DllImport("user32.dll", SetLastError = true)]
		private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

		[DllImport("user32.dll")]
		private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern bool DestroyWindow(IntPtr hwnd);

		[DllImport("user32.dll")]
		private static extern void PostQuitMessage(int nExitCode);

		[DllImport("user32.dll")]
		private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

		[DllImport("user32.dll")]
		private static extern bool TranslateMessage(ref MSG lpMsg);

		[DllImport("user32.dll")]
		private static extern IntPtr DispatchMessage(ref MSG lpMsg);

		[DllImport("user32.dll")]
		private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

		[DllImport("user32.dll")]
		private static extern bool AdjustWindowRect(ref RECT lpRect, int dwStyle, bool bMenu);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

		[DllImport("user32.dll")]
		private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

		[DllImport("user32.dll")]
		private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

		[DllImport("user32.dll")]
		private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT lpPaint);

		[DllImport("user32.dll")]
		private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
	}
}
