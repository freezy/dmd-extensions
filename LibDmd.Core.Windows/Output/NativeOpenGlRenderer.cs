using System;
using System.Runtime.InteropServices;
using LibDmd.Frame;
using LibDmd.Output.Virtual.Dmd;

namespace LibDmd.Output.NativeWindow
{
	internal sealed class NativeOpenGlRenderer : IDisposable
	{
		private readonly IntPtr _hwnd;
		private readonly Dimensions _size;
		private IntPtr _hdc;
		private IntPtr _hglrc;
		private VirtualDmdOpenGlPipeline _pipeline;
		private bool _disposed;

		public NativeOpenGlRenderer(IntPtr hwnd, Dimensions size)
		{
			_hwnd = hwnd;
			_size = size;
			Initialize();
		}

		public void Render(byte[] rgba)
		{
			if (_disposed || _hglrc == IntPtr.Zero || rgba == null) {
				return;
			}

			if (!GetClientRect(_hwnd, out var client)) {
				return;
			}

			var clientWidth = Math.Max(1, client.Right - client.Left);
			var clientHeight = Math.Max(1, client.Bottom - client.Top);
			var scale = Math.Min(clientWidth / (float)_size.Width, clientHeight / (float)_size.Height);
			var renderWidth = _size.Width * scale;
			var renderHeight = _size.Height * scale;
			var offsetX = (clientWidth - renderWidth) * 0.5f;
			var offsetY = (clientHeight - renderHeight) * 0.5f;

			wglMakeCurrent(_hdc, _hglrc);
			_pipeline.Render(rgba, clientWidth, clientHeight, offsetX, offsetY, renderWidth, renderHeight);
			SwapBuffers(_hdc);
		}

		public void Dispose()
		{
			if (_disposed) {
				return;
			}

			_disposed = true;
			if (_hglrc != IntPtr.Zero) {
				wglMakeCurrent(_hdc, _hglrc);
				_pipeline?.Dispose();
				wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
				wglDeleteContext(_hglrc);
				_hglrc = IntPtr.Zero;
			}

			if (_hdc != IntPtr.Zero) {
				ReleaseDC(_hwnd, _hdc);
				_hdc = IntPtr.Zero;
			}
		}

		private void Initialize()
		{
			_hdc = GetDC(_hwnd);
			if (_hdc == IntPtr.Zero) {
				throw new InvalidOperationException($"Could not get native DMD window device context. Win32 error: {Marshal.GetLastWin32Error()}.");
			}

			var pfd = new PixelFormatDescriptor {
				nSize = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
				nVersion = 1,
				dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
				iPixelType = PFD_TYPE_RGBA,
				cColorBits = 32,
				cDepthBits = 0,
				iLayerType = PFD_MAIN_PLANE
			};

			var pixelFormat = ChoosePixelFormat(_hdc, ref pfd);
			if (pixelFormat == 0 || !SetPixelFormat(_hdc, pixelFormat, ref pfd)) {
				throw new InvalidOperationException($"Could not set native DMD OpenGL pixel format. Win32 error: {Marshal.GetLastWin32Error()}.");
			}

			_hglrc = wglCreateContext(_hdc);
			if (_hglrc == IntPtr.Zero || !wglMakeCurrent(_hdc, _hglrc)) {
				throw new InvalidOperationException($"Could not create native DMD OpenGL context. Win32 error: {Marshal.GetLastWin32Error()}.");
			}

			_pipeline = new VirtualDmdOpenGlPipeline(_size);
		}

		private const uint PFD_DOUBLEBUFFER = 0x00000001;
		private const uint PFD_DRAW_TO_WINDOW = 0x00000004;
		private const byte PFD_MAIN_PLANE = 0;
		private const uint PFD_SUPPORT_OPENGL = 0x00000020;
		private const byte PFD_TYPE_RGBA = 0;

		[StructLayout(LayoutKind.Sequential)]
		private struct PixelFormatDescriptor
		{
			public ushort nSize;
			public ushort nVersion;
			public uint dwFlags;
			public byte iPixelType;
			public byte cColorBits;
			public byte cRedBits;
			public byte cRedShift;
			public byte cGreenBits;
			public byte cGreenShift;
			public byte cBlueBits;
			public byte cBlueShift;
			public byte cAlphaBits;
			public byte cAlphaShift;
			public byte cAccumBits;
			public byte cAccumRedBits;
			public byte cAccumGreenBits;
			public byte cAccumBlueBits;
			public byte cAccumAlphaBits;
			public byte cDepthBits;
			public byte cStencilBits;
			public byte cAuxBuffers;
			public byte iLayerType;
			public byte bReserved;
			public uint dwLayerMask;
			public uint dwVisibleMask;
			public uint dwDamageMask;
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
		private static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("user32.dll")]
		private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("gdi32.dll", SetLastError = true)]
		private static extern int ChoosePixelFormat(IntPtr hdc, ref PixelFormatDescriptor ppfd);

		[DllImport("gdi32.dll", SetLastError = true)]
		private static extern bool SetPixelFormat(IntPtr hdc, int format, ref PixelFormatDescriptor ppfd);

		[DllImport("gdi32.dll")]
		private static extern bool SwapBuffers(IntPtr hdc);

		[DllImport("opengl32.dll")]
		private static extern IntPtr wglCreateContext(IntPtr hdc);

		[DllImport("opengl32.dll")]
		private static extern bool wglDeleteContext(IntPtr hglrc);

		[DllImport("opengl32.dll")]
		private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
	}
}
