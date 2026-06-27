using System;
using System.Runtime.InteropServices;
using System.Threading;
using LibDmd.Frame;
using LibDmd.Output.Virtual.Dmd;
using NLog;

namespace LibDmd.Output.NativeWindow
{
	internal sealed class SdlNativeWindowDestination : INativeWindowBackend
	{
		private const int Scale = 4;
		private const int ResizeGripSize = 18;
		private readonly object _frameLock = new object();
		private readonly ManualResetEventSlim _windowReady = new ManualResetEventSlim();
		private readonly Thread _windowThread;
		private readonly Dimensions _size;
		private readonly byte[] _renderBuffer;
		private int _windowLeft;
		private int _windowTop;
		private int _windowWidth;
		private int _windowHeight;
		private bool _stayOnTop;
		private VirtualDmdRenderStyle _renderStyle;
		private VirtualDmdOpenGlPipeline _pipeline;
		private IntPtr _window;
		private IntPtr _context;
		private bool _disposed;
		private bool _isMovingOrSizing;
		private bool _dragging;
		private bool _resizing;
		private int _dragStartMouseX;
		private int _dragStartMouseY;
		private int _dragStartLeft;
		private int _dragStartTop;
		private int _dragStartWidth;
		private int _dragStartHeight;
		private int _renderPending;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public SdlNativeWindowDestination(int width, int height, int windowLeft, int windowTop, int windowWidth, int windowHeight, bool stayOnTop, VirtualDmdRenderStyle renderStyle)
		{
			_size = new Dimensions(width, height);
			_renderBuffer = new byte[_size.Surface * 4];
			_windowLeft = windowLeft;
			_windowTop = windowTop;
			_windowWidth = windowWidth > 0 ? windowWidth : width * Scale;
			_windowHeight = windowHeight > 0 ? windowHeight : height * Scale;
			_stayOnTop = stayOnTop;
			_renderStyle = renderStyle ?? VirtualDmdRenderStyle.Default;
			_windowThread = new Thread(WindowThreadMain) {
				IsBackground = true,
				Name = "LibDmd SDL Native Window"
			};
			_windowThread.Start();
			_windowReady.Wait(TimeSpan.FromSeconds(2));
		}

		public bool IsAvailable => _window != IntPtr.Zero && !_disposed;
		public int WindowLeft => _windowLeft;
		public int WindowTop => _windowTop;
		public int WindowWidth => _windowWidth;
		public int WindowHeight => _windowHeight;
		public bool WindowStayOnTop => _stayOnTop;
		public bool IsMovingOrSizing => _isMovingOrSizing;

		public void ConfigureWindow(int left, int top, int width, int height, bool stayOnTop)
		{
			_windowLeft = left;
			_windowTop = top;
			_windowWidth = width > 0 ? width : _size.Width * Scale;
			_windowHeight = height > 0 ? height : _size.Height * Scale;
			_stayOnTop = stayOnTop;
			var window = _window;
			if (window == IntPtr.Zero) {
				return;
			}

			SDL_SetWindowPosition(window, _windowLeft, _windowTop);
			SDL_SetWindowSize(window, _windowWidth, _windowHeight);
			SDL_SetWindowAlwaysOnTop(window, _stayOnTop ? SDL_TRUE : SDL_FALSE);
			RequestPaint();
		}

		public void ConfigureRenderStyle(VirtualDmdRenderStyle renderStyle)
		{
			_renderStyle = renderStyle ?? VirtualDmdRenderStyle.Default;
			_pipeline?.SetStyle(_renderStyle);
			RequestPaint();
		}

		public void Render(byte[] rgba)
		{
			if (_disposed || rgba == null) {
				return;
			}

			lock (_frameLock) {
				Buffer.BlockCopy(rgba, 0, _renderBuffer, 0, Math.Min(rgba.Length, _renderBuffer.Length));
			}
			RequestPaint();
		}

		public void Dispose()
		{
			if (_disposed) {
				return;
			}

			_disposed = true;
			if (!_windowThread.Join(TimeSpan.FromSeconds(1))) {
				Logger.Warn("[DMD] SDL native DMD window thread did not stop within 1s.");
			}
			_windowReady.Dispose();
		}

		private void RequestPaint()
		{
			Interlocked.Exchange(ref _renderPending, 1);
		}

		private void WindowThreadMain()
		{
			try {
				InitializeWindow();
				_windowReady.Set();
				RequestPaint();

				while (!_disposed) {
					while (SDL_PollEvent(out var ev) != 0) {
						HandleEvent(ev);
					}

					if (Interlocked.Exchange(ref _renderPending, 0) != 0 && !_isMovingOrSizing) {
						RenderOpenGl();
					}

					SDL_Delay(8);
				}
			} catch (Exception exception) {
				Logger.Warn(exception, "[DMD] SDL native DMD window failed.");
				_windowReady.Set();
			} finally {
				DisposeWindow();
			}
		}

		private void InitializeWindow()
		{
			SDL_SetHint(SDL_HINT_NO_SIGNAL_HANDLERS, "1");
			if (SDL_Init(SDL_INIT_VIDEO) != 0) {
				throw new InvalidOperationException($"SDL_Init failed: {GetSdlError()}");
			}

			SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, 2);
			SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, 1);
			SDL_GL_SetAttribute(SDL_GL_DOUBLEBUFFER, 1);
			SDL_GL_SetAttribute(SDL_GL_ACCELERATED_VISUAL, 1);

			var flags = SDL_WINDOW_OPENGL | SDL_WINDOW_SHOWN | SDL_WINDOW_BORDERLESS | SDL_WINDOW_RESIZABLE | SDL_WINDOW_ALLOW_HIGHDPI;
			if (_stayOnTop) {
				flags |= SDL_WINDOW_ALWAYS_ON_TOP;
			}

			_window = SDL_CreateWindow("VPE DMD", _windowLeft, _windowTop, _windowWidth, _windowHeight, flags);
			if (_window == IntPtr.Zero) {
				throw new InvalidOperationException($"SDL_CreateWindow failed: {GetSdlError()}");
			}

			_context = SDL_GL_CreateContext(_window);
			if (_context == IntPtr.Zero) {
				throw new InvalidOperationException($"SDL_GL_CreateContext failed: {GetSdlError()}");
			}

			SDL_GL_MakeCurrent(_window, _context);
			SDL_GL_SetSwapInterval(1);
			_pipeline = new VirtualDmdOpenGlPipeline(_size, _renderStyle, SDL_GL_GetProcAddress);
			ReadWindowLayout();
			Logger.Info("[DMD] SDL native DMD window created.");
		}

		private void DisposeWindow()
		{
			if (_context != IntPtr.Zero) {
				SDL_GL_MakeCurrent(_window, _context);
				_pipeline?.Dispose();
				_pipeline = null;
				SDL_GL_DeleteContext(_context);
				_context = IntPtr.Zero;
			}

			if (_window != IntPtr.Zero) {
				SDL_DestroyWindow(_window);
				_window = IntPtr.Zero;
			}

			SDL_QuitSubSystem(SDL_INIT_VIDEO);
		}

		private void RenderOpenGl()
		{
			if (_window == IntPtr.Zero || _context == IntPtr.Zero || _pipeline == null) {
				return;
			}

			SDL_GL_MakeCurrent(_window, _context);
			SDL_GL_GetDrawableSize(_window, out var drawableWidth, out var drawableHeight);
			drawableWidth = Math.Max(1, drawableWidth);
			drawableHeight = Math.Max(1, drawableHeight);
			var scale = Math.Min(drawableWidth / (float)_size.Width, drawableHeight / (float)_size.Height);
			var renderWidth = _size.Width * scale;
			var renderHeight = _size.Height * scale;
			var offsetX = (drawableWidth - renderWidth) * 0.5f;
			var offsetY = (drawableHeight - renderHeight) * 0.5f;

			byte[] frame;
			lock (_frameLock) {
				frame = new byte[_renderBuffer.Length];
				Buffer.BlockCopy(_renderBuffer, 0, frame, 0, frame.Length);
			}

			_pipeline.SetStyle(_renderStyle);
			_pipeline.Render(frame, drawableWidth, drawableHeight, offsetX, offsetY, renderWidth, renderHeight);
			SDL_GL_SwapWindow(_window);
		}

		private void HandleEvent(SdlEvent ev)
		{
			if (_window == IntPtr.Zero) {
				return;
			}

			switch (ev.Type) {
				case SDL_QUIT:
					_disposed = true;
					break;
				case SDL_WINDOWEVENT:
					if (ev.Window.WindowId != SDL_GetWindowID(_window)) {
						return;
					}
					HandleWindowEvent(ev.Window);
					break;
				case SDL_MOUSEBUTTONDOWN:
					if (ev.Button.WindowId == SDL_GetWindowID(_window) && ev.Button.Button == SDL_BUTTON_LEFT) {
						BeginMoveOrResize(ev.Button.X, ev.Button.Y);
					}
					break;
				case SDL_MOUSEBUTTONUP:
					if (ev.Button.WindowId == SDL_GetWindowID(_window) && ev.Button.Button == SDL_BUTTON_LEFT) {
						EndMoveOrResize();
					}
					break;
				case SDL_MOUSEMOTION:
					if (ev.Motion.WindowId == SDL_GetWindowID(_window)) {
						MoveOrResize(ev.Motion.X, ev.Motion.Y);
					}
					break;
			}
		}

		private void HandleWindowEvent(SdlWindowEvent ev)
		{
			switch (ev.Event) {
				case SDL_WINDOWEVENT_MOVED:
				case SDL_WINDOWEVENT_SIZE_CHANGED:
				case SDL_WINDOWEVENT_RESIZED:
					ReadWindowLayout();
					RequestPaint();
					break;
				case SDL_WINDOWEVENT_CLOSE:
					_disposed = true;
					break;
			}
		}

		private void BeginMoveOrResize(int mouseX, int mouseY)
		{
			ReadWindowLayout();
			_dragStartMouseX = mouseX;
			_dragStartMouseY = mouseY;
			_dragStartLeft = _windowLeft;
			_dragStartTop = _windowTop;
			_dragStartWidth = _windowWidth;
			_dragStartHeight = _windowHeight;
			_resizing = mouseX >= _windowWidth - ResizeGripSize && mouseY >= _windowHeight - ResizeGripSize;
			_dragging = !_resizing;
			_isMovingOrSizing = true;
		}

		private void MoveOrResize(int mouseX, int mouseY)
		{
			if (!_dragging && !_resizing) {
				return;
			}

			if (_dragging) {
				_windowLeft = _dragStartLeft + mouseX - _dragStartMouseX;
				_windowTop = _dragStartTop + mouseY - _dragStartMouseY;
				SDL_SetWindowPosition(_window, _windowLeft, _windowTop);
			} else {
				_windowWidth = Math.Max(_size.Width, _dragStartWidth + mouseX - _dragStartMouseX);
				_windowHeight = Math.Max(_size.Height, _dragStartHeight + mouseY - _dragStartMouseY);
				SDL_SetWindowSize(_window, _windowWidth, _windowHeight);
			}
		}

		private void EndMoveOrResize()
		{
			if (!_dragging && !_resizing) {
				return;
			}

			_dragging = false;
			_resizing = false;
			_isMovingOrSizing = false;
			ReadWindowLayout();
			RequestPaint();
		}

		private void ReadWindowLayout()
		{
			if (_window == IntPtr.Zero) {
				return;
			}

			SDL_GetWindowPosition(_window, out _windowLeft, out _windowTop);
			SDL_GetWindowSize(_window, out _windowWidth, out _windowHeight);
			_windowWidth = Math.Max(1, _windowWidth);
			_windowHeight = Math.Max(1, _windowHeight);
		}

		private static string GetSdlError()
		{
			var error = SDL_GetError();
			return error == IntPtr.Zero ? "unknown SDL error" : Marshal.PtrToStringAnsi(error);
		}

		private const uint SDL_INIT_VIDEO = 0x00000020;
		private const uint SDL_WINDOW_OPENGL = 0x00000002;
		private const uint SDL_WINDOW_SHOWN = 0x00000004;
		private const uint SDL_WINDOW_BORDERLESS = 0x00000010;
		private const uint SDL_WINDOW_RESIZABLE = 0x00000020;
		private const uint SDL_WINDOW_ALLOW_HIGHDPI = 0x00002000;
		private const uint SDL_WINDOW_ALWAYS_ON_TOP = 0x00008000;
		private const int SDL_FALSE = 0;
		private const int SDL_TRUE = 1;
		private const int SDL_GL_DOUBLEBUFFER = 5;
		private const int SDL_GL_ACCELERATED_VISUAL = 15;
		private const int SDL_GL_CONTEXT_MAJOR_VERSION = 17;
		private const int SDL_GL_CONTEXT_MINOR_VERSION = 18;
		private const uint SDL_QUIT = 0x100;
		private const uint SDL_WINDOWEVENT = 0x200;
		private const uint SDL_MOUSEMOTION = 0x400;
		private const uint SDL_MOUSEBUTTONDOWN = 0x401;
		private const uint SDL_MOUSEBUTTONUP = 0x402;
		private const byte SDL_BUTTON_LEFT = 1;
		private const byte SDL_WINDOWEVENT_MOVED = 4;
		private const byte SDL_WINDOWEVENT_RESIZED = 5;
		private const byte SDL_WINDOWEVENT_SIZE_CHANGED = 6;
		private const byte SDL_WINDOWEVENT_CLOSE = 14;
		private const string SDL_HINT_NO_SIGNAL_HANDLERS = "SDL_NO_SIGNAL_HANDLERS";

		[StructLayout(LayoutKind.Explicit, Size = 56)]
		private struct SdlEvent
		{
			[FieldOffset(0)] public uint Type;
			[FieldOffset(0)] public SdlWindowEvent Window;
			[FieldOffset(0)] public SdlMouseButtonEvent Button;
			[FieldOffset(0)] public SdlMouseMotionEvent Motion;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SdlWindowEvent
		{
			public uint Type;
			public uint Timestamp;
			public uint WindowId;
			public byte Event;
			public byte Padding1;
			public byte Padding2;
			public byte Padding3;
			public int Data1;
			public int Data2;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SdlMouseButtonEvent
		{
			public uint Type;
			public uint Timestamp;
			public uint WindowId;
			public uint Which;
			public byte Button;
			public byte State;
			public byte Clicks;
			public byte Padding1;
			public int X;
			public int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SdlMouseMotionEvent
		{
			public uint Type;
			public uint Timestamp;
			public uint WindowId;
			public uint Which;
			public uint State;
			public int X;
			public int Y;
			public int XRel;
			public int YRel;
		}

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern int SDL_Init(uint flags);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_QuitSubSystem(uint flags);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern int SDL_SetHint(string name, string value);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern int SDL_GL_SetAttribute(int attr, int value);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr SDL_CreateWindow(string title, int x, int y, int w, int h, uint flags);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_DestroyWindow(IntPtr window);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr SDL_GL_CreateContext(IntPtr window);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern int SDL_GL_MakeCurrent(IntPtr window, IntPtr context);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_GL_DeleteContext(IntPtr context);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern int SDL_GL_SetSwapInterval(int interval);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_GL_SwapWindow(IntPtr window);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr SDL_GL_GetProcAddress(string proc);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_GL_GetDrawableSize(IntPtr window, out int w, out int h);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern int SDL_PollEvent(out SdlEvent ev);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_Delay(uint ms);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern uint SDL_GetWindowID(IntPtr window);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_GetWindowPosition(IntPtr window, out int x, out int y);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_GetWindowSize(IntPtr window, out int w, out int h);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_SetWindowPosition(IntPtr window, int x, int y);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_SetWindowSize(IntPtr window, int w, int h);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern void SDL_SetWindowAlwaysOnTop(IntPtr window, int onTop);

		[DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr SDL_GetError();
	}
}
