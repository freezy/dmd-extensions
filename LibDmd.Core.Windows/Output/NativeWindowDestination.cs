using System;
using System.Runtime.InteropServices;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Output.Virtual.Dmd;
using NLog;

namespace LibDmd.Output.NativeWindow
{
	public sealed class NativeWindowDestination : IGray2Destination, IGray4Destination, IGray8Destination, IRgb24Destination, IRgb565Destination, IFixedSizeDestination, INativeDmdWindow
	{
		private const int Scale = 4;
		private readonly Dimensions _size;
		private readonly byte[] _rgba;
		private readonly INativeWindowBackend _backend;
		private Color _color = Color.FromRgb(255, 88, 0);
		private bool _disposed;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
			_backend = CreateBackend(width, height, windowLeft, windowTop, windowWidth, windowHeight, stayOnTop, renderStyle);
		}

		/// <summary>
		/// Strongly-typed constructor used by the cross-platform <see cref="NativeDmdWindow.TryCreate"/>
		/// factory so consumers never touch reflection or the GL-coupled style type.
		/// </summary>
		public NativeWindowDestination(int width, int height, DmdWindowLayout layout, DmdWindowStyle style)
			: this(width, height,
				(layout ?? new DmdWindowLayout()).Left,
				(layout ?? new DmdWindowLayout()).Top,
				(layout ?? new DmdWindowLayout()).Width,
				(layout ?? new DmdWindowLayout()).Height,
				(layout ?? new DmdWindowLayout()).StayOnTop,
				ToRenderStyle(style))
		{
		}

		public string Name => "Native DMD Window";
		public bool IsAvailable => !_disposed && _backend.IsAvailable;
		public bool NeedsDuplicateFrames => false;
		public bool NeedsIdentificationFrames => false;
		public Dimensions FixedSize => _size;
		public bool DmdAllowHdScaling => false;
		public int WindowLeft => _backend.WindowLeft;
		public int WindowTop => _backend.WindowTop;
		public int WindowWidth => _backend.WindowWidth;
		public int WindowHeight => _backend.WindowHeight;
		public bool WindowStayOnTop => _backend.WindowStayOnTop;
		public bool IsMovingOrSizing => _backend.IsMovingOrSizing;

		public void RenderGray2(DmdFrame frame) => RenderGray(frame, 3);
		public void RenderGray4(DmdFrame frame) => RenderGray(frame, 15);
		public void RenderGray8(DmdFrame frame) => RenderGray(frame, 255);

		public void RenderRgb565(DmdFrame frame)
		{
			if (_disposed || frame?.Data == null) {
				return;
			}

			for (var i = 0; i < _size.Surface; i++) {
				var value = frame.Data[i * 2] | (frame.Data[i * 2 + 1] << 8);
				var r = (byte)(((value >> 11) & 0x1f) * 255 / 31);
				var g = (byte)(((value >> 5) & 0x3f) * 255 / 63);
				var b = (byte)((value & 0x1f) * 255 / 31);
				WriteRgba(i, r, g, b);
			}
			_backend.Render(_rgba);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			if (_disposed || frame?.Data == null) {
				return;
			}

			for (var i = 0; i < _size.Surface; i++) {
				WriteRgba(i, frame.Data[i * 3], frame.Data[i * 3 + 1], frame.Data[i * 3 + 2]);
			}
			_backend.Render(_rgba);
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
			_backend.ConfigureWindow(left, top, width, height, stayOnTop);
		}

		public void ConfigureRenderStyle(VirtualDmdRenderStyle renderStyle)
		{
			_backend.ConfigureRenderStyle(renderStyle);
		}

		// --- INativeDmdWindow ---

		/// <summary>
		/// False: the current backends (Win32 message loop, SDL loop) own their own thread.
		/// The host-pumped backend (Phase B / macOS) flips this to true.
		/// </summary>
		public bool RequiresHostPump => false;

		public void ConfigureWindow(DmdWindowLayout layout)
		{
			if (layout == null) {
				return;
			}
			_backend.ConfigureWindow(layout.Left, layout.Top, layout.Width, layout.Height, layout.StayOnTop);
		}

		public void ConfigureStyle(DmdWindowStyle style)
		{
			_backend.ConfigureRenderStyle(ToRenderStyle(style));
		}

		public void Pump()
		{
			// No-op: self-driven backends render on their own thread. Host-driven backends override this.
		}

		private static VirtualDmdRenderStyle ToRenderStyle(DmdWindowStyle style)
		{
			if (style == null) {
				return VirtualDmdRenderStyle.Default;
			}

			return new VirtualDmdRenderStyle {
				DotSize = style.DotSize,
				DotRounding = style.DotRounding,
				DotSharpness = style.DotSharpness,
				UnlitDotR = style.UnlitDotR,
				UnlitDotG = style.UnlitDotG,
				UnlitDotB = style.UnlitDotB,
				Brightness = style.Brightness,
				DotGlow = style.DotGlow,
				BackGlow = style.BackGlow,
				Gamma = style.Gamma,
				GlassR = style.GlassR,
				GlassG = style.GlassG,
				GlassB = style.GlassB,
				GlassLighting = style.GlassLighting,
			};
		}

		public void ClearDisplay()
		{
			Array.Clear(_rgba, 0, _rgba.Length);
			_backend.Render(_rgba);
		}

		public void Dispose()
		{
			if (_disposed) {
				return;
			}

			_disposed = true;
			_backend.Dispose();
		}

		private void RenderGray(DmdFrame frame, int maxValue)
		{
			if (_disposed || frame?.Data == null) {
				return;
			}

			for (var i = 0; i < _size.Surface; i++) {
				var intensity = frame.Data[i] / (float)maxValue;
				WriteRgba(
					i,
					(byte)(_color.R * intensity),
					(byte)(_color.G * intensity),
					(byte)(_color.B * intensity));
			}
			_backend.Render(_rgba);
		}

		private void WriteRgba(int pixel, byte r, byte g, byte b)
		{
			var offset = pixel * 4;
			_rgba[offset] = r;
			_rgba[offset + 1] = g;
			_rgba[offset + 2] = b;
			_rgba[offset + 3] = 255;
		}

		private static INativeWindowBackend CreateBackend(int width, int height, int windowLeft, int windowTop, int windowWidth, int windowHeight, bool stayOnTop, VirtualDmdRenderStyle renderStyle)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				return new Win32NativeWindowDestination(width, height, windowLeft, windowTop, windowWidth, windowHeight, stayOnTop, renderStyle);
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				return new SdlNativeWindowDestination(width, height, windowLeft, windowTop, windowWidth, windowHeight, stayOnTop, renderStyle);
			}

			Logger.Warn($"[DMD] Native DMD window is not supported on {RuntimeInformation.OSDescription}.");
			return new NullNativeWindowBackend(width, height, windowLeft, windowTop, windowWidth, windowHeight, stayOnTop);
		}
	}
}
