using System;
using System.Runtime.InteropServices;
using System.Text;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.NativeWindow
{
	internal sealed class NativeOpenGlRenderer : IDisposable
	{
		private const byte DefaultAlpha = 255;
		private readonly IntPtr _hwnd;
		private readonly Dimensions _size;
		private IntPtr _hdc;
		private IntPtr _hglrc;
		private bool _disposed;
		private bool _textureCreated;
		private bool _shaderAvailable;
		private uint _texture;
		private uint _program;
		private int _textureUniform;
		private int _dmdSizeUniform;
		private int _unlitDotUniform;

		private GlCreateShader _glCreateShader;
		private GlShaderSource _glShaderSource;
		private GlCompileShader _glCompileShader;
		private GlGetShaderiv _glGetShaderiv;
		private GlGetShaderInfoLog _glGetShaderInfoLog;
		private GlDeleteShader _glDeleteShader;
		private GlCreateProgram _glCreateProgram;
		private GlAttachShader _glAttachShader;
		private GlLinkProgram _glLinkProgram;
		private GlGetProgramiv _glGetProgramiv;
		private GlGetProgramInfoLog _glGetProgramInfoLog;
		private GlUseProgram _glUseProgram;
		private GlGetUniformLocation _glGetUniformLocation;
		private GlUniform1i _glUniform1i;
		private GlUniform2f _glUniform2f;
		private GlUniform3f _glUniform3f;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public NativeOpenGlRenderer(IntPtr hwnd, Dimensions size)
		{
			_hwnd = hwnd;
			_size = size;
			Initialize();
		}

		public void Render(byte[] rgb)
		{
			if (_disposed || _hglrc == IntPtr.Zero || rgb == null) {
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
			PrepareViewport(clientWidth, clientHeight);

			if (_shaderAvailable) {
				RenderShader(rgb, offsetX, offsetY, renderWidth, renderHeight);
			} else {
				RenderImmediate(rgb, scale, offsetX, offsetY);
			}

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
				if (_program != 0 && _glUseProgram != null) {
					_glUseProgram(0);
				}
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

			try {
				LoadShaderFunctions();
				_program = CreateProgram(VertexShader, DmdFragmentShader);
				_textureUniform = _glGetUniformLocation(_program, "dmdTexture");
				_dmdSizeUniform = _glGetUniformLocation(_program, "dmdSize");
				_unlitDotUniform = _glGetUniformLocation(_program, "unlitDot");
				_shaderAvailable = true;
				Logger.Info("[DMD] Native OpenGL shader renderer initialized.");
			} catch (Exception exception) {
				_shaderAvailable = false;
				Logger.Warn(exception, "[DMD] Native OpenGL shader renderer unavailable; using immediate dot renderer.");
			}
		}

		private void PrepareViewport(int clientWidth, int clientHeight)
		{
			glViewport(0, 0, clientWidth, clientHeight);
			glClearColor(0f, 0f, 0f, 1f);
			glClear(GL_COLOR_BUFFER_BIT);
			glMatrixMode(GL_PROJECTION);
			glLoadIdentity();
			glOrtho(0, clientWidth, clientHeight, 0, -1, 1);
			glMatrixMode(GL_MODELVIEW);
			glLoadIdentity();
			glDisable(GL_DEPTH_TEST);
		}

		private void RenderShader(byte[] rgb, float offsetX, float offsetY, float renderWidth, float renderHeight)
		{
			UploadTexture(rgb);
			glEnable(GL_TEXTURE_2D);
			glBindTexture(GL_TEXTURE_2D, _texture);
			_glUseProgram(_program);
			_glUniform1i(_textureUniform, 0);
			_glUniform2f(_dmdSizeUniform, _size.Width, _size.Height);
			_glUniform3f(_unlitDotUniform, 0.026f, 0.006f, 0f);

			glBegin(GL_QUADS);
			glTexCoord2f(0f, 0f);
			glVertex2f(offsetX, offsetY);
			glTexCoord2f(1f, 0f);
			glVertex2f(offsetX + renderWidth, offsetY);
			glTexCoord2f(1f, 1f);
			glVertex2f(offsetX + renderWidth, offsetY + renderHeight);
			glTexCoord2f(0f, 1f);
			glVertex2f(offsetX, offsetY + renderHeight);
			glEnd();

			_glUseProgram(0);
			glDisable(GL_TEXTURE_2D);
		}

		private void RenderImmediate(byte[] rgb, float scale, float offsetX, float offsetY)
		{
			glDisable(GL_TEXTURE_2D);
			var dotSize = Math.Max(1f, scale * 0.72f);
			var dotPad = (scale - dotSize) * 0.5f;
			var unlitR = 0.028f;
			var unlitG = 0.008f;
			var unlitB = 0f;

			glBegin(GL_QUADS);
			for (var y = 0; y < _size.Height; y++) {
				for (var x = 0; x < _size.Width; x++) {
					var pixel = (y * _size.Width + x) * 4;
					var r = rgb[pixel] / (float)DefaultAlpha;
					var g = rgb[pixel + 1] / (float)DefaultAlpha;
					var b = rgb[pixel + 2] / (float)DefaultAlpha;
					var max = Math.Max(r, Math.Max(g, b));
					if (max < 0.002f) {
						r = unlitR;
						g = unlitG;
						b = unlitB;
					}

					DrawDot(
						offsetX + x * scale + dotPad,
						offsetY + y * scale + dotPad,
						dotSize,
						r,
						g,
						b);
				}
			}
			glEnd();
		}

		private void UploadTexture(byte[] rgb)
		{
			if (_texture == 0) {
				var textures = new uint[1];
				glGenTextures(1, textures);
				_texture = textures[0];
			}

			glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
			glBindTexture(GL_TEXTURE_2D, _texture);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_NEAREST);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_NEAREST);

			if (_textureCreated) {
				glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, _size.Width, _size.Height, GL_RGBA, GL_UNSIGNED_BYTE, rgb);
			} else {
				glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, _size.Width, _size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, rgb);
				_textureCreated = true;
			}
		}

		private uint CreateProgram(string vertexSource, string fragmentSource)
		{
			var vertexShader = CompileShader(GL_VERTEX_SHADER, vertexSource);
			var fragmentShader = CompileShader(GL_FRAGMENT_SHADER, fragmentSource);
			var program = _glCreateProgram();
			_glAttachShader(program, vertexShader);
			_glAttachShader(program, fragmentShader);
			_glLinkProgram(program);
			_glGetProgramiv(program, GL_LINK_STATUS, out var status);
			if (status == 0) {
				throw new InvalidOperationException("OpenGL DMD shader program link failed: " + GetProgramLog(program));
			}

			_glDeleteShader(vertexShader);
			_glDeleteShader(fragmentShader);
			return program;
		}

		private uint CompileShader(uint type, string source)
		{
			var shader = _glCreateShader(type);
			var sources = new[] { source };
			var lengths = new[] { source.Length };
			_glShaderSource(shader, sources.Length, sources, lengths);
			_glCompileShader(shader);
			_glGetShaderiv(shader, GL_COMPILE_STATUS, out var status);
			if (status == 0) {
				throw new InvalidOperationException("OpenGL DMD shader compilation failed: " + GetShaderLog(shader));
			}

			return shader;
		}

		private string GetShaderLog(uint shader)
		{
			_glGetShaderiv(shader, GL_INFO_LOG_LENGTH, out var length);
			var log = new StringBuilder(Math.Max(1, length));
			_glGetShaderInfoLog(shader, log.Capacity, out _, log);
			return log.ToString();
		}

		private string GetProgramLog(uint program)
		{
			_glGetProgramiv(program, GL_INFO_LOG_LENGTH, out var length);
			var log = new StringBuilder(Math.Max(1, length));
			_glGetProgramInfoLog(program, log.Capacity, out _, log);
			return log.ToString();
		}

		private void LoadShaderFunctions()
		{
			_glCreateShader = LoadGlFunction<GlCreateShader>("glCreateShader");
			_glShaderSource = LoadGlFunction<GlShaderSource>("glShaderSource");
			_glCompileShader = LoadGlFunction<GlCompileShader>("glCompileShader");
			_glGetShaderiv = LoadGlFunction<GlGetShaderiv>("glGetShaderiv");
			_glGetShaderInfoLog = LoadGlFunction<GlGetShaderInfoLog>("glGetShaderInfoLog");
			_glDeleteShader = LoadGlFunction<GlDeleteShader>("glDeleteShader");
			_glCreateProgram = LoadGlFunction<GlCreateProgram>("glCreateProgram");
			_glAttachShader = LoadGlFunction<GlAttachShader>("glAttachShader");
			_glLinkProgram = LoadGlFunction<GlLinkProgram>("glLinkProgram");
			_glGetProgramiv = LoadGlFunction<GlGetProgramiv>("glGetProgramiv");
			_glGetProgramInfoLog = LoadGlFunction<GlGetProgramInfoLog>("glGetProgramInfoLog");
			_glUseProgram = LoadGlFunction<GlUseProgram>("glUseProgram");
			_glGetUniformLocation = LoadGlFunction<GlGetUniformLocation>("glGetUniformLocation");
			_glUniform1i = LoadGlFunction<GlUniform1i>("glUniform1i");
			_glUniform2f = LoadGlFunction<GlUniform2f>("glUniform2f");
			_glUniform3f = LoadGlFunction<GlUniform3f>("glUniform3f");
		}

		private static T LoadGlFunction<T>(string name) where T : Delegate
		{
			var address = wglGetProcAddress(name);
			if (address == IntPtr.Zero || address == new IntPtr(1) || address == new IntPtr(2) || address == new IntPtr(3) || address == new IntPtr(-1)) {
				var module = LoadLibrary("opengl32.dll");
				address = GetProcAddress(module, name);
			}

			if (address == IntPtr.Zero) {
				throw new InvalidOperationException($"OpenGL function {name} is unavailable.");
			}

			return Marshal.GetDelegateForFunctionPointer<T>(address);
		}

		private static void DrawDot(float x, float y, float size, float r, float g, float b)
		{
			glColor3f(r, g, b);
			glVertex2f(x, y);
			glVertex2f(x + size, y);
			glVertex2f(x + size, y + size);
			glVertex2f(x, y + size);
		}

		private const string VertexShader = @"
#version 120
varying vec2 dmdUv;

void main()
{
	dmdUv = gl_MultiTexCoord0.xy;
	gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
}";

		private const string DmdFragmentShader = @"
#version 120
varying vec2 dmdUv;

uniform sampler2D dmdTexture;
uniform vec2 dmdSize;
uniform vec3 unlitDot;

const float dotSize = 0.72;
const float dotRounding = 0.42;
const float sharpMax = 0.055;
const float sharpMin = -0.31;
const float dotGlow = 0.18;

float udRoundBox(vec2 p, float b, float r)
{
	vec2 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

vec3 dotSample(vec2 ofs)
{
	vec2 nearest = (floor(dmdUv * dmdSize) + ofs) / dmdSize;
	vec2 pos = 2.0 * (fract(dmdUv * dmdSize) - ofs);
	float dot = smoothstep(sharpMax, sharpMin, udRoundBox(pos, dotSize, dotRounding * dotSize));
	vec3 dmd = texture2D(dmdTexture, nearest).rgb;
	return (dmd + unlitDot) * dot;
}

void main()
{
	vec3 color = dotSample(vec2(0.5, 0.5));
	vec2 pixel = 1.0 / dmdSize;
	vec3 glow = texture2D(dmdTexture, dmdUv + vec2(-pixel.x, 0.0)).rgb;
	glow += texture2D(dmdTexture, dmdUv + vec2(pixel.x, 0.0)).rgb;
	glow += texture2D(dmdTexture, dmdUv + vec2(0.0, -pixel.y)).rgb;
	glow += texture2D(dmdTexture, dmdUv + vec2(0.0, pixel.y)).rgb;
	color += glow * dotGlow;
	color = pow(color, vec3(1.0 / 1.15));
	gl_FragColor = vec4(color, 1.0);
}";

		private const uint GL_CLAMP = 0x2900;
		private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
		private const uint GL_COMPILE_STATUS = 0x8B81;
		private const uint GL_DEPTH_TEST = 0x0B71;
		private const uint GL_FRAGMENT_SHADER = 0x8B30;
		private const uint GL_INFO_LOG_LENGTH = 0x8B84;
		private const uint GL_LINK_STATUS = 0x8B82;
		private const uint GL_MODELVIEW = 0x1700;
		private const uint GL_NEAREST = 0x2600;
		private const uint GL_PROJECTION = 0x1701;
		private const uint GL_QUADS = 0x0007;
		private const uint GL_RGBA = 0x1908;
		private const uint GL_TEXTURE_2D = 0x0DE1;
		private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
		private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
		private const uint GL_TEXTURE_WRAP_S = 0x2802;
		private const uint GL_TEXTURE_WRAP_T = 0x2803;
		private const uint GL_UNPACK_ALIGNMENT = 0x0CF5;
		private const uint GL_UNSIGNED_BYTE = 0x1401;
		private const uint GL_VERTEX_SHADER = 0x8B31;
		private const uint PFD_DOUBLEBUFFER = 0x00000001;
		private const uint PFD_DRAW_TO_WINDOW = 0x00000004;
		private const byte PFD_MAIN_PLANE = 0;
		private const uint PFD_SUPPORT_OPENGL = 0x00000020;
		private const byte PFD_TYPE_RGBA = 0;

		private delegate uint GlCreateShader(uint shaderType);
		private delegate void GlShaderSource(uint shader, int count, string[] source, int[] length);
		private delegate void GlCompileShader(uint shader);
		private delegate void GlGetShaderiv(uint shader, uint pname, out int value);
		private delegate void GlGetShaderInfoLog(uint shader, int maxLength, out int length, StringBuilder infoLog);
		private delegate void GlDeleteShader(uint shader);
		private delegate uint GlCreateProgram();
		private delegate void GlAttachShader(uint program, uint shader);
		private delegate void GlLinkProgram(uint program);
		private delegate void GlGetProgramiv(uint program, uint pname, out int value);
		private delegate void GlGetProgramInfoLog(uint program, int maxLength, out int length, StringBuilder infoLog);
		private delegate void GlUseProgram(uint program);
		private delegate int GlGetUniformLocation(uint program, string name);
		private delegate void GlUniform1i(int location, int value);
		private delegate void GlUniform2f(int location, float value0, float value1);
		private delegate void GlUniform3f(int location, float value0, float value1, float value2);

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

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("opengl32.dll")]
		private static extern IntPtr wglCreateContext(IntPtr hdc);

		[DllImport("opengl32.dll")]
		private static extern bool wglDeleteContext(IntPtr hglrc);

		[DllImport("opengl32.dll")]
		private static extern IntPtr wglGetProcAddress(string lpszProc);

		[DllImport("opengl32.dll")]
		private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

		[DllImport("opengl32.dll")]
		private static extern void glBegin(uint mode);

		[DllImport("opengl32.dll")]
		private static extern void glBindTexture(uint target, uint texture);

		[DllImport("opengl32.dll")]
		private static extern void glClear(uint mask);

		[DllImport("opengl32.dll")]
		private static extern void glClearColor(float red, float green, float blue, float alpha);

		[DllImport("opengl32.dll")]
		private static extern void glColor3f(float red, float green, float blue);

		[DllImport("opengl32.dll")]
		private static extern void glDisable(uint cap);

		[DllImport("opengl32.dll")]
		private static extern void glEnable(uint cap);

		[DllImport("opengl32.dll")]
		private static extern void glEnd();

		[DllImport("opengl32.dll")]
		private static extern void glGenTextures(int n, uint[] textures);

		[DllImport("opengl32.dll")]
		private static extern void glLoadIdentity();

		[DllImport("opengl32.dll")]
		private static extern void glMatrixMode(uint mode);

		[DllImport("opengl32.dll")]
		private static extern void glOrtho(double left, double right, double bottom, double top, double zNear, double zFar);

		[DllImport("opengl32.dll")]
		private static extern void glPixelStorei(uint pname, int param);

		[DllImport("opengl32.dll")]
		private static extern void glTexCoord2f(float s, float t);

		[DllImport("opengl32.dll")]
		private static extern void glTexImage2D(uint target, int level, uint internalFormat, int width, int height, int border, uint format, uint type, byte[] pixels);

		[DllImport("opengl32.dll")]
		private static extern void glTexParameteri(uint target, uint pname, int param);

		[DllImport("opengl32.dll")]
		private static extern void glTexSubImage2D(uint target, int level, int xoffset, int yoffset, int width, int height, uint format, uint type, byte[] pixels);

		[DllImport("opengl32.dll")]
		private static extern void glVertex2f(float x, float y);

		[DllImport("opengl32.dll")]
		private static extern void glViewport(int x, int y, int width, int height);
	}
}
