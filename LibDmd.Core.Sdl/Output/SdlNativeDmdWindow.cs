using System;
using System.Runtime.InteropServices;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Output.NativeWindow;
using NLog;

namespace LibDmd.Output.NativeWindow
{
	/// <summary>
	/// Cross-platform (macOS / Linux / mobile-capable) host-pumped native DMD window.
	/// </summary>
	/// <remarks>
	/// THREADING — the whole point of this backend:
	/// <list type="bullet">
	/// <item><b>Construction</b> runs on the host's main thread (the VPE bridge creates destinations
	/// from Unity's main thread). SDL init, window + GL ES context creation all happen here, which is
	/// mandatory on macOS (AppKit/SDL windowing is main-thread-only).</item>
	/// <item><b>RenderGrayX / RenderRgbX</b> are called by the RenderGraph on the DMD <i>worker</i>
	/// thread. They ONLY convert into a locked back-buffer — never touch GL.</item>
	/// <item><b><see cref="Pump"/></b> is called once per frame by the host on the main thread. It
	/// makes the GL context current, pumps SDL events, renders the latest buffered frame and swaps.
	/// <see cref="RequiresHostPump"/> is therefore <c>true</c>.</item>
	/// </list>
	///
	/// MAC GO/NO-GO ITEMS (the things to validate first on a real Mac):
	/// 1. GL-context coexistence with Unity: we save/restore the current GL context around our render
	///    (best-effort — restoring a non-SDL context, i.e. Unity's, is not fully portable). This is the
	///    single biggest risk; if Unity's view goes black, this is where to look.
	/// 2. SDL2 + an ANGLE runtime (libEGL/libGLESv2) must be on the load path; we request a GL ES profile.
	/// 3. Image orientation / letterboxing (see <see cref="GlesDmdPipeline"/>).
	/// Glass overlay is not yet ported to the GL ES pipeline (dots + glow are); it's a follow-on.
	/// </remarks>
	internal sealed class SdlNativeDmdWindow :
		INativeDmdWindow, IGray2Destination, IGray4Destination, IGray8Destination, IRgb24Destination, IRgb565Destination, IFixedSizeDestination
	{
		private const int Scale = 4;

		private readonly Dimensions _size;
		private readonly object _frameLock = new object();
		private readonly object _stateLock = new object();

		private byte[] _backRgba;
		private byte[] _frontRgba;
		private bool _hasFrame;
		private bool _haveContent;
		private Color _color = Color.FromRgb(255, 88, 0);

		private IntPtr _window;
		private IntPtr _glContext;
		private IntPtr _eventBuffer;
		private GlesDmdPipeline _pipeline;
		private bool _initialized;
		private bool _disposed;

		// Cached geometry (read on the main thread in Pump, surfaced to the bridge).
		private int _windowLeft;
		private int _windowTop;
		private int _windowWidth;
		private int _windowHeight;
		private bool _stayOnTop;

		// Pending config from the main thread, applied at the top of Pump.
		private DmdWindowLayout _pendingLayout;
		private DmdWindowStyle _pendingStyle;
		private DmdWindowStyle _style;

		// Manual borderless drag.
		private bool _dragging;
		private int _dragMouseStartX;
		private int _dragMouseStartY;
		private int _dragWindowStartX;
		private int _dragWindowStartY;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static int _sdlInitialized;

		public SdlNativeDmdWindow(int width, int height, DmdWindowLayout layout, DmdWindowStyle style)
		{
			_size = new Dimensions(width, height);
			_backRgba = new byte[_size.Surface * 4];
			_frontRgba = new byte[_size.Surface * 4];
			layout = layout ?? new DmdWindowLayout(100, 100, width * Scale, height * Scale, false);
			_style = style ?? new DmdWindowStyle();
			_windowLeft = layout.Left;
			_windowTop = layout.Top;
			_windowWidth = layout.Width > 0 ? layout.Width : width * Scale;
			_windowHeight = layout.Height > 0 ? layout.Height : height * Scale;
			_stayOnTop = layout.StayOnTop;

			Initialize();
		}

		public string Name => "Native DMD Window (SDL/GL ES)";
		public bool IsAvailable => _initialized && !_disposed && _window != IntPtr.Zero;
		public bool NeedsDuplicateFrames => false;
		public bool NeedsIdentificationFrames => false;
		public Dimensions FixedSize => _size;
		public bool DmdAllowHdScaling => false;

		public int WindowLeft => _windowLeft;
		public int WindowTop => _windowTop;
		public int WindowWidth => _windowWidth;
		public int WindowHeight => _windowHeight;
		public bool WindowStayOnTop => _stayOnTop;
		public bool IsMovingOrSizing => _dragging;
		public bool RequiresHostPump => true;

		// --- IDestination render methods (worker thread): buffer only, never touch GL ----------------

		public void RenderGray2(DmdFrame frame) => RenderGray(frame, 3);
		public void RenderGray4(DmdFrame frame) => RenderGray(frame, 15);
		public void RenderGray8(DmdFrame frame) => RenderGray(frame, 255);

		public void RenderRgb24(DmdFrame frame)
		{
			if (_disposed || frame?.Data == null) {
				return;
			}
			lock (_frameLock) {
				for (var i = 0; i < _size.Surface; i++) {
					WriteRgba(i, frame.Data[i * 3], frame.Data[i * 3 + 1], frame.Data[i * 3 + 2]);
				}
				_hasFrame = true;
			}
		}

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
				_hasFrame = true;
			}
		}

		private void RenderGray(DmdFrame frame, int maxValue)
		{
			if (_disposed || frame?.Data == null) {
				return;
			}
			var color = _color;
			lock (_frameLock) {
				for (var i = 0; i < _size.Surface; i++) {
					var intensity = frame.Data[i] / (float)maxValue;
					WriteRgba(i, (byte)(color.R * intensity), (byte)(color.G * intensity), (byte)(color.B * intensity));
				}
				_hasFrame = true;
			}
		}

		public void SetColor(Color color) => _color = color;

		public void SetPalette(Color[] colors)
		{
			if (colors != null && colors.Length > 0) {
				_color = colors[colors.Length - 1];
			}
		}

		public void ClearColor() => _color = Color.FromRgb(255, 88, 0);
		public void ClearPalette() { }

		public void ClearDisplay()
		{
			lock (_frameLock) {
				Array.Clear(_backRgba, 0, _backRgba.Length);
				_hasFrame = true;
			}
		}

		private void WriteRgba(int pixel, byte r, byte g, byte b)
		{
			var offset = pixel * 4;
			_backRgba[offset] = r;
			_backRgba[offset + 1] = g;
			_backRgba[offset + 2] = b;
			_backRgba[offset + 3] = 255;
		}

		// --- INativeDmdWindow config (main thread): stage pending, apply in Pump --------------------

		public void ConfigureWindow(DmdWindowLayout layout)
		{
			if (layout == null) {
				return;
			}
			lock (_stateLock) {
				_pendingLayout = layout;
			}
		}

		public void ConfigureStyle(DmdWindowStyle style)
		{
			lock (_stateLock) {
				_pendingStyle = style ?? new DmdWindowStyle();
			}
		}

		// --- Pump (main thread) ---------------------------------------------------------------------

		public void Pump()
		{
			if (_disposed || !_initialized) {
				return;
			}

			var prevContext = Sdl.SDL_GL_GetCurrentContext();
			var prevWindow = Sdl.SDL_GL_GetCurrentWindow();
			if (Sdl.SDL_GL_MakeCurrent(_window, _glContext) != 0) {
				Logger.Warn($"[DMD] SDL_GL_MakeCurrent failed: {Sdl.GetError()}");
				return;
			}

			try {
				ApplyPending();
				PollEvents();
				ReadWindowGeometry();

				bool render;
				lock (_frameLock) {
					if (_hasFrame) {
						var swap = _frontRgba;
						_frontRgba = _backRgba;
						_backRgba = swap;
						_hasFrame = false;
						_haveContent = true;
					}
					render = _haveContent;
				}

				if (render) {
					Sdl.SDL_GL_GetDrawableSize(_window, out var dw, out var dh);
					_pipeline.Render(_frontRgba, dw, dh);
					Sdl.SDL_GL_SwapWindow(_window);
				}
			} finally {
				// Best-effort restore so we don't leave our context bound where the host (Unity) expects
				// its own. Restoring a non-SDL context isn't fully portable — see the class remarks.
				if (prevContext != IntPtr.Zero && prevContext != _glContext) {
					Sdl.SDL_GL_MakeCurrent(prevWindow, prevContext);
				}
			}
		}

		private void ApplyPending()
		{
			DmdWindowLayout layout;
			DmdWindowStyle style;
			lock (_stateLock) {
				layout = _pendingLayout;
				_pendingLayout = null;
				style = _pendingStyle;
				_pendingStyle = null;
			}

			if (layout != null) {
				_stayOnTop = layout.StayOnTop;
				Sdl.SDL_SetWindowPosition(_window, layout.Left, layout.Top);
				if (layout.Width > 0 && layout.Height > 0) {
					Sdl.SDL_SetWindowSize(_window, layout.Width, layout.Height);
				}
				Sdl.SDL_SetWindowAlwaysOnTop(_window, layout.StayOnTop ? Sdl.SDL_TRUE : Sdl.SDL_FALSE);
			}

			if (style != null) {
				_style = style;
				_pipeline.SetStyle(style);
			}
		}

		private void PollEvents()
		{
			while (Sdl.SDL_PollEvent(_eventBuffer) != 0) {
				var type = (uint)Marshal.ReadInt32(_eventBuffer, 0);
				switch (type) {
					case Sdl.SDL_MOUSEBUTTONDOWN:
						if (Marshal.ReadByte(_eventBuffer, Sdl.EventOffsetButton) == Sdl.SDL_BUTTON_LEFT) {
							Sdl.SDL_GetGlobalMouseState(out _dragMouseStartX, out _dragMouseStartY);
							Sdl.SDL_GetWindowPosition(_window, out _dragWindowStartX, out _dragWindowStartY);
							_dragging = true;
						}
						break;
					case Sdl.SDL_MOUSEBUTTONUP:
						if (Marshal.ReadByte(_eventBuffer, Sdl.EventOffsetButton) == Sdl.SDL_BUTTON_LEFT) {
							_dragging = false;
						}
						break;
					case Sdl.SDL_MOUSEMOTION:
						if (_dragging) {
							Sdl.SDL_GetGlobalMouseState(out var mx, out var my);
							Sdl.SDL_SetWindowPosition(_window, _dragWindowStartX + (mx - _dragMouseStartX), _dragWindowStartY + (my - _dragMouseStartY));
						}
						break;
				}
			}
		}

		private void ReadWindowGeometry()
		{
			Sdl.SDL_GetWindowPosition(_window, out _windowLeft, out _windowTop);
			Sdl.SDL_GetWindowSize(_window, out _windowWidth, out _windowHeight);
		}

		// --- Init / teardown ------------------------------------------------------------------------

		private void Initialize()
		{
			EnsureSdlVideo();

			Sdl.SDL_SetHint(Sdl.SDL_HINT_OPENGL_ES_DRIVER, "1");
			Sdl.SDL_GL_SetAttribute(Sdl.SDL_GL_CONTEXT_PROFILE_MASK, Sdl.SDL_GL_CONTEXT_PROFILE_ES);
			Sdl.SDL_GL_SetAttribute(Sdl.SDL_GL_CONTEXT_MAJOR_VERSION, 2);
			Sdl.SDL_GL_SetAttribute(Sdl.SDL_GL_CONTEXT_MINOR_VERSION, 0);
			Sdl.SDL_GL_SetAttribute(Sdl.SDL_GL_DOUBLEBUFFER, 1);
			Sdl.SDL_GL_SetAttribute(Sdl.SDL_GL_RED_SIZE, 8);
			Sdl.SDL_GL_SetAttribute(Sdl.SDL_GL_GREEN_SIZE, 8);
			Sdl.SDL_GL_SetAttribute(Sdl.SDL_GL_BLUE_SIZE, 8);
			Sdl.SDL_GL_SetAttribute(Sdl.SDL_GL_ALPHA_SIZE, 8);

			var flags = Sdl.SDL_WINDOW_OPENGL | Sdl.SDL_WINDOW_SHOWN | Sdl.SDL_WINDOW_BORDERLESS
				| Sdl.SDL_WINDOW_RESIZABLE | Sdl.SDL_WINDOW_ALLOW_HIGHDPI;
			if (_stayOnTop) {
				flags |= Sdl.SDL_WINDOW_ALWAYS_ON_TOP;
			}

			_window = Sdl.SDL_CreateWindow("VPE DMD", _windowLeft, _windowTop, _windowWidth, _windowHeight, flags);
			if (_window == IntPtr.Zero) {
				throw new InvalidOperationException($"SDL_CreateWindow failed: {Sdl.GetError()}");
			}

			var prevContext = Sdl.SDL_GL_GetCurrentContext();
			var prevWindow = Sdl.SDL_GL_GetCurrentWindow();
			_glContext = Sdl.SDL_GL_CreateContext(_window);
			if (_glContext == IntPtr.Zero) {
				Sdl.SDL_DestroyWindow(_window);
				_window = IntPtr.Zero;
				throw new InvalidOperationException($"SDL_GL_CreateContext failed: {Sdl.GetError()}");
			}

			Sdl.SDL_GL_MakeCurrent(_window, _glContext);
			Sdl.SDL_GL_SetSwapInterval(1);

			_pipeline = new GlesDmdPipeline(_size, _style, Sdl.SDL_GL_GetProcAddress);
			var ok = _pipeline.Initialize();

			// Restore whatever context was current before we constructed (best-effort; see remarks).
			if (prevContext != IntPtr.Zero) {
				Sdl.SDL_GL_MakeCurrent(prevWindow, prevContext);
			}

			if (!ok) {
				_pipeline.Dispose();
				Sdl.SDL_GL_DeleteContext(_glContext);
				Sdl.SDL_DestroyWindow(_window);
				_glContext = IntPtr.Zero;
				_window = IntPtr.Zero;
				throw new InvalidOperationException("GL ES DMD pipeline failed to initialize.");
			}

			_eventBuffer = Marshal.AllocHGlobal(Sdl.EventSize);
			_initialized = true;
			Logger.Info($"[DMD] SDL native DMD window created ({_size.Width}x{_size.Height}) at {_windowLeft},{_windowTop} ({_windowWidth}x{_windowHeight}).");
		}

		private static void EnsureSdlVideo()
		{
			if (System.Threading.Interlocked.Exchange(ref _sdlInitialized, 1) == 1) {
				Sdl.SDL_InitSubSystem(Sdl.SDL_INIT_VIDEO);
				return;
			}
			if (Sdl.SDL_Init(Sdl.SDL_INIT_VIDEO) != 0) {
				_sdlInitialized = 0;
				throw new InvalidOperationException($"SDL_Init(VIDEO) failed: {Sdl.GetError()}");
			}
		}

		public void Dispose()
		{
			if (_disposed) {
				return;
			}
			_disposed = true;

			// Teardown must run on the thread owning the context; the bridge disposes pipelines from the
			// main thread (EnsurePipeline / OnDestroy), which is the pump thread, so this is correct.
			if (_initialized) {
				Sdl.SDL_GL_MakeCurrent(_window, _glContext);
				_pipeline?.Dispose();
				Sdl.SDL_GL_DeleteContext(_glContext);
				Sdl.SDL_DestroyWindow(_window);
				_glContext = IntPtr.Zero;
				_window = IntPtr.Zero;
			}
			if (_eventBuffer != IntPtr.Zero) {
				Marshal.FreeHGlobal(_eventBuffer);
				_eventBuffer = IntPtr.Zero;
			}
		}
	}
}
