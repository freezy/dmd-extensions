using System;
using System.Runtime.InteropServices;
using System.Text;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.Virtual.Dmd
{
	public sealed class VirtualDmdOpenGlPipeline : IDisposable
	{
		private const byte DefaultAlpha = 255;
		private readonly Dimensions _size;
		private bool _sourceTextureCreated;
		private bool _framebuffersCreated;
		private bool _dmdRendererAvailable;
		private byte[] _uploadBuffer;
		private uint _sourceTexture;
		private uint _dotGlowTexture;
		private uint _backGlowTexture;
		private uint _tempTexture;
		private uint _dotGlowFramebuffer;
		private uint _backGlowFramebuffer;
		private uint _tempFramebuffer;
		private uint _blur2Program;
		private uint _blur12Program;
		private uint _dmdProgram;
		private int _blur2TextureUniform;
		private int _blur2DirectionUniform;
		private int _blur12TextureUniform;
		private int _blur12DirectionUniform;
		private int _dmdTextureUniform;
		private int _dmdDotGlowUniform;
		private int _dmdBackGlowUniform;
		private int _dmdSizeUniform;
		private int _dmdUnlitDotUniform;

		private GlActiveTexture _glActiveTexture;
		private GlAttachShader _glAttachShader;
		private GlBindFramebuffer _glBindFramebuffer;
		private GlCheckFramebufferStatus _glCheckFramebufferStatus;
		private GlCompileShader _glCompileShader;
		private GlCreateProgram _glCreateProgram;
		private GlCreateShader _glCreateShader;
		private GlDeleteFramebuffers _glDeleteFramebuffers;
		private GlDeleteShader _glDeleteShader;
		private GlFramebufferTexture2D _glFramebufferTexture2D;
		private GlGenFramebuffers _glGenFramebuffers;
		private GlGetProgramInfoLog _glGetProgramInfoLog;
		private GlGetProgramiv _glGetProgramiv;
		private GlGetShaderInfoLog _glGetShaderInfoLog;
		private GlGetShaderiv _glGetShaderiv;
		private GlGetUniformLocation _glGetUniformLocation;
		private GlLinkProgram _glLinkProgram;
		private GlShaderSource _glShaderSource;
		private GlUniform1i _glUniform1i;
		private GlUniform2f _glUniform2f;
		private GlUniform3f _glUniform3f;
		private GlUseProgram _glUseProgram;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdOpenGlPipeline(Dimensions size)
		{
			_size = size;
			try {
				LoadRendererFunctions();
				_blur2Program = CreateProgram(PassthroughVertexShader, BlurFragmentPrelude + "void main() { gl_FragColor = vec4(blur_level_2(texture, uv, direction).rgb, 1.0); }");
				_blur12Program = CreateProgram(PassthroughVertexShader, BlurFragmentPrelude + "void main() { gl_FragColor = vec4(blur_level_12(texture, uv, direction).rgb, 1.0); }");
				_dmdProgram = CreateProgram(PassthroughVertexShader, DmdFragmentShader);

				_blur2TextureUniform = _glGetUniformLocation(_blur2Program, "texture");
				_blur2DirectionUniform = _glGetUniformLocation(_blur2Program, "direction");
				_blur12TextureUniform = _glGetUniformLocation(_blur12Program, "texture");
				_blur12DirectionUniform = _glGetUniformLocation(_blur12Program, "direction");
				_dmdTextureUniform = _glGetUniformLocation(_dmdProgram, "dmdTexture");
				_dmdDotGlowUniform = _glGetUniformLocation(_dmdProgram, "dmdDotGlow");
				_dmdBackGlowUniform = _glGetUniformLocation(_dmdProgram, "dmdBackGlow");
				_dmdSizeUniform = _glGetUniformLocation(_dmdProgram, "dmdSize");
				_dmdUnlitDotUniform = _glGetUniformLocation(_dmdProgram, "unlitDot");

				_dmdRendererAvailable = true;
				Logger.Info("[DMD] Shared dmdext OpenGL renderer initialized.");
			} catch (Exception exception) {
				_dmdRendererAvailable = false;
				Logger.Warn(exception, "[DMD] Shared dmdext OpenGL renderer unavailable; using immediate dot renderer.");
			}
		}

		public void Render(byte[] rgba, int clientWidth, int clientHeight, float offsetX, float offsetY, float renderWidth, float renderHeight)
		{
			PrepareViewport(clientWidth, clientHeight);
			if (_dmdRendererAvailable) {
				RenderDmdExtPipeline(rgba, clientWidth, clientHeight, offsetX, offsetY, renderWidth, renderHeight);
			} else {
				var scale = renderWidth / _size.Width;
				RenderImmediate(rgba, scale, offsetX, offsetY);
			}
		}

		public void Dispose()
		{
			if (_dmdRendererAvailable) {
				_glUseProgram(0);
				var fbos = new[] { _dotGlowFramebuffer, _backGlowFramebuffer, _tempFramebuffer };
				_glDeleteFramebuffers(fbos.Length, fbos);
			}
		}

		private void PrepareViewport(int clientWidth, int clientHeight)
		{
			glViewport(0, 0, clientWidth, clientHeight);
			glClearColor(0f, 0f, 0f, 1f);
			glClear(GL_COLOR_BUFFER_BIT);
			SetOrthographicProjection(clientWidth, clientHeight);
			glDisable(GL_DEPTH_TEST);
			glDisable(GL_TEXTURE_2D);
		}

		private void RenderDmdExtPipeline(byte[] rgba, int clientWidth, int clientHeight, float offsetX, float offsetY, float renderWidth, float renderHeight)
		{
			UploadSourceTexture(rgba);
			EnsureFramebuffers();

			RunBlurPass(_blur2Program, _blur2TextureUniform, _blur2DirectionUniform, _sourceTexture, _tempFramebuffer, 1.0f / _size.Width, 0f);
			RunBlurPass(_blur2Program, _blur2TextureUniform, _blur2DirectionUniform, _tempTexture, _dotGlowFramebuffer, 0f, 1.0f / _size.Height);
			RunBlurPass(_blur12Program, _blur12TextureUniform, _blur12DirectionUniform, _dotGlowTexture, _tempFramebuffer, 1.0f / _size.Width, 0f);
			RunBlurPass(_blur12Program, _blur12TextureUniform, _blur12DirectionUniform, _tempTexture, _backGlowFramebuffer, 0f, 1.0f / _size.Height);

			_glBindFramebuffer(GL_FRAMEBUFFER, 0);
			SetOrthographicProjection(clientWidth, clientHeight);
			_glUseProgram(_dmdProgram);

			BindTextureUnit(0, _sourceTexture);
			BindTextureUnit(1, _dotGlowTexture);
			BindTextureUnit(2, _backGlowTexture);
			_glUniform1i(_dmdTextureUniform, 0);
			_glUniform1i(_dmdDotGlowUniform, 1);
			_glUniform1i(_dmdBackGlowUniform, 2);
			_glUniform2f(_dmdSizeUniform, _size.Width, _size.Height);
			_glUniform3f(_dmdUnlitDotUniform, 0.018f, 0.004f, 0f);
			DrawDmdQuad(offsetX, offsetY, renderWidth, renderHeight);

			_glUseProgram(0);
			_glActiveTexture(GL_TEXTURE0);
			glDisable(GL_TEXTURE_2D);
		}

		private void RunBlurPass(uint program, int textureUniform, int directionUniform, uint inputTexture, uint framebuffer, float directionX, float directionY)
		{
			_glBindFramebuffer(GL_FRAMEBUFFER, framebuffer);
			SetOrthographicProjection(_size.Width, _size.Height);
			glClear(GL_COLOR_BUFFER_BIT);
			_glUseProgram(program);
			BindTextureUnit(0, inputTexture);
			_glUniform1i(textureUniform, 0);
			_glUniform2f(directionUniform, directionX, directionY);
			DrawDmdQuad(0f, 0f, _size.Width, _size.Height);
			_glUseProgram(0);
		}

		private void EnsureFramebuffers()
		{
			if (_framebuffersCreated) {
				return;
			}

			_dotGlowTexture = CreateRenderTexture();
			_backGlowTexture = CreateRenderTexture();
			_tempTexture = CreateRenderTexture();
			_dotGlowFramebuffer = CreateFramebuffer(_dotGlowTexture);
			_backGlowFramebuffer = CreateFramebuffer(_backGlowTexture);
			_tempFramebuffer = CreateFramebuffer(_tempTexture);
			_framebuffersCreated = true;
		}

		private uint CreateRenderTexture()
		{
			var textures = new uint[1];
			glGenTextures(1, textures);
			glBindTexture(GL_TEXTURE_2D, textures[0]);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
			glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, _size.Width, _size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
			return textures[0];
		}

		private uint CreateFramebuffer(uint texture)
		{
			var framebuffers = new uint[1];
			_glGenFramebuffers(1, framebuffers);
			_glBindFramebuffer(GL_FRAMEBUFFER, framebuffers[0]);
			_glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, texture, 0);
			var status = _glCheckFramebufferStatus(GL_FRAMEBUFFER);
			if (status != GL_FRAMEBUFFER_COMPLETE) {
				throw new InvalidOperationException($"OpenGL framebuffer setup failed. Status: 0x{status:x}.");
			}
			_glBindFramebuffer(GL_FRAMEBUFFER, 0);
			return framebuffers[0];
		}

		private void UploadSourceTexture(byte[] rgba)
		{
			if (_sourceTexture == 0) {
				var textures = new uint[1];
				glGenTextures(1, textures);
				_sourceTexture = textures[0];
			}

			var upload = GetOpenGlUploadBuffer(rgba);
			glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
			glBindTexture(GL_TEXTURE_2D, _sourceTexture);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_NEAREST);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_NEAREST);

			if (_sourceTextureCreated) {
				glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, _size.Width, _size.Height, GL_RGBA, GL_UNSIGNED_BYTE, upload);
			} else {
				glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, _size.Width, _size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, upload);
				_sourceTextureCreated = true;
			}
		}

		private byte[] GetOpenGlUploadBuffer(byte[] rgba)
		{
			var stride = _size.Width * 4;
			if (_uploadBuffer == null || _uploadBuffer.Length != rgba.Length) {
				_uploadBuffer = new byte[rgba.Length];
			}

			for (var y = 0; y < _size.Height; y++) {
				Buffer.BlockCopy(rgba, y * stride, _uploadBuffer, (_size.Height - 1 - y) * stride, stride);
			}

			return _uploadBuffer;
		}

		private void BindTextureUnit(uint unit, uint texture)
		{
			_glActiveTexture(GL_TEXTURE0 + unit);
			glEnable(GL_TEXTURE_2D);
			glBindTexture(GL_TEXTURE_2D, texture);
		}

		private static void DrawDmdQuad(float x, float y, float width, float height)
		{
			glBegin(GL_QUADS);
			glTexCoord2f(0f, 1f);
			glVertex2f(x, y);
			glTexCoord2f(0f, 0f);
			glVertex2f(x, y + height);
			glTexCoord2f(1f, 0f);
			glVertex2f(x + width, y + height);
			glTexCoord2f(1f, 1f);
			glVertex2f(x + width, y);
			glEnd();
		}

		private static void SetOrthographicProjection(int width, int height)
		{
			glViewport(0, 0, width, height);
			glMatrixMode(GL_PROJECTION);
			glLoadIdentity();
			glOrtho(0, width, height, 0, -1, 1);
			glMatrixMode(GL_MODELVIEW);
			glLoadIdentity();
		}

		private void RenderImmediate(byte[] rgba, float scale, float offsetX, float offsetY)
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
					var r = rgba[pixel] / (float)DefaultAlpha;
					var g = rgba[pixel + 1] / (float)DefaultAlpha;
					var b = rgba[pixel + 2] / (float)DefaultAlpha;
					var max = Math.Max(r, Math.Max(g, b));
					if (max < 0.002f) {
						r = unlitR;
						g = unlitG;
						b = unlitB;
					}

					DrawDot(offsetX + x * scale + dotPad, offsetY + y * scale + dotPad, dotSize, r, g, b);
				}
			}
			glEnd();
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
				throw new InvalidOperationException("OpenGL program link failed: " + GetProgramLog(program));
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
				throw new InvalidOperationException("OpenGL shader compilation failed: " + GetShaderLog(shader));
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

		private void LoadRendererFunctions()
		{
			_glActiveTexture = LoadGlFunction<GlActiveTexture>("glActiveTexture");
			_glAttachShader = LoadGlFunction<GlAttachShader>("glAttachShader");
			_glBindFramebuffer = LoadGlFunction<GlBindFramebuffer>("glBindFramebuffer");
			_glCheckFramebufferStatus = LoadGlFunction<GlCheckFramebufferStatus>("glCheckFramebufferStatus");
			_glCompileShader = LoadGlFunction<GlCompileShader>("glCompileShader");
			_glCreateProgram = LoadGlFunction<GlCreateProgram>("glCreateProgram");
			_glCreateShader = LoadGlFunction<GlCreateShader>("glCreateShader");
			_glDeleteFramebuffers = LoadGlFunction<GlDeleteFramebuffers>("glDeleteFramebuffers");
			_glDeleteShader = LoadGlFunction<GlDeleteShader>("glDeleteShader");
			_glFramebufferTexture2D = LoadGlFunction<GlFramebufferTexture2D>("glFramebufferTexture2D");
			_glGenFramebuffers = LoadGlFunction<GlGenFramebuffers>("glGenFramebuffers");
			_glGetProgramInfoLog = LoadGlFunction<GlGetProgramInfoLog>("glGetProgramInfoLog");
			_glGetProgramiv = LoadGlFunction<GlGetProgramiv>("glGetProgramiv");
			_glGetShaderInfoLog = LoadGlFunction<GlGetShaderInfoLog>("glGetShaderInfoLog");
			_glGetShaderiv = LoadGlFunction<GlGetShaderiv>("glGetShaderiv");
			_glGetUniformLocation = LoadGlFunction<GlGetUniformLocation>("glGetUniformLocation");
			_glLinkProgram = LoadGlFunction<GlLinkProgram>("glLinkProgram");
			_glShaderSource = LoadGlFunction<GlShaderSource>("glShaderSource");
			_glUniform1i = LoadGlFunction<GlUniform1i>("glUniform1i");
			_glUniform2f = LoadGlFunction<GlUniform2f>("glUniform2f");
			_glUniform3f = LoadGlFunction<GlUniform3f>("glUniform3f");
			_glUseProgram = LoadGlFunction<GlUseProgram>("glUseProgram");
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

		private const string PassthroughVertexShader = @"
#version 120
varying vec2 uv;

void main()
{
	uv = gl_MultiTexCoord0.xy;
	gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
}";

		private const string BlurFragmentPrelude = @"
#version 120
varying vec2 uv;

uniform sampler2D texture;
uniform vec2 direction;

vec4 blur_level_2(sampler2D image, vec2 coord, vec2 blurDirection) {
	vec4 color = vec4(0.0);
	vec2 off1 = blurDirection;
	color += texture2D(image, coord) * 0.5;
	color += texture2D(image, coord + off1) * 0.25;
	color += texture2D(image, coord - off1) * 0.25;
	return color;
}

vec4 blur_level_12(sampler2D image, vec2 coord, vec2 blurDirection) {
	vec4 color = vec4(0.0);
	vec2 off1 = vec2(1.3846153846) * blurDirection;
	vec2 off2 = vec2(3.2307692308) * blurDirection;
	color += texture2D(image, coord) * 0.2270270270;
	color += texture2D(image, coord + off1) * 0.3162162162;
	color += texture2D(image, coord - off1) * 0.3162162162;
	color += texture2D(image, coord + off2) * 0.0702702703;
	color += texture2D(image, coord - off2) * 0.0702702703;
	return color;
}
";

		private const string DmdFragmentShader = @"
#version 120
varying vec2 uv;

uniform sampler2D dmdTexture;
uniform sampler2D dmdDotGlow;
uniform sampler2D dmdBackGlow;
uniform vec2 dmdSize;
uniform vec3 unlitDot;

const float dotSize = 0.92000;
const float dotRounding = 1.00000;
const float sharpMax = 0.19400;
const float sharpMin = -0.19400;
const float brightness = 0.95000;
const float backGlow = 0.10000;
const float dotGlow = 0.18000;
const float gamma = 1.05000;

float udRoundBox(vec2 p, float b, float r)
{
	vec2 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

vec3 computeDotColor(vec2 ofs)
{
	vec2 nearest = (floor(uv * dmdSize) + ofs) / dmdSize;
	vec2 pos = 2.0 * (fract(uv * dmdSize) - ofs);
	float dot = smoothstep(sharpMax, sharpMin, udRoundBox(pos, dotSize, dotRounding * dotSize));
	vec3 dmd = texture2D(dmdTexture, nearest).rgb + unlitDot;
	return dmd * dot;
}

void main()
{
	vec3 dotColor = vec3(0.0);
	for(int x = -1; x <= 1; x++) {
		for(int y = -1; y <= 1; y++) {
			dotColor = max(dotColor, computeDotColor(vec2(float(x) + 0.5, float(y) + 0.5)));
		}
	}

	dotColor += texture2D(dmdDotGlow, uv).rgb * dotGlow;
	dotColor += texture2D(dmdBackGlow, uv).rgb * backGlow;
	dotColor *= brightness;
	dotColor = pow(dotColor, vec3(1.0 / gamma));
	gl_FragColor = vec4(dotColor, 1.0);
}";

		private const uint GL_CLAMP = 0x2900;
		private const uint GL_COLOR_ATTACHMENT0 = 0x8CE0;
		private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
		private const uint GL_COMPILE_STATUS = 0x8B81;
		private const uint GL_DEPTH_TEST = 0x0B71;
		private const uint GL_FRAGMENT_SHADER = 0x8B30;
		private const uint GL_FRAMEBUFFER = 0x8D40;
		private const uint GL_FRAMEBUFFER_COMPLETE = 0x8CD5;
		private const uint GL_INFO_LOG_LENGTH = 0x8B84;
		private const uint GL_LINEAR = 0x2601;
		private const uint GL_LINK_STATUS = 0x8B82;
		private const uint GL_MODELVIEW = 0x1700;
		private const uint GL_NEAREST = 0x2600;
		private const uint GL_PROJECTION = 0x1701;
		private const uint GL_QUADS = 0x0007;
		private const uint GL_RGBA = 0x1908;
		private const uint GL_TEXTURE0 = 0x84C0;
		private const uint GL_TEXTURE_2D = 0x0DE1;
		private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
		private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
		private const uint GL_TEXTURE_WRAP_S = 0x2802;
		private const uint GL_TEXTURE_WRAP_T = 0x2803;
		private const uint GL_UNPACK_ALIGNMENT = 0x0CF5;
		private const uint GL_UNSIGNED_BYTE = 0x1401;
		private const uint GL_VERTEX_SHADER = 0x8B31;

		private delegate void GlActiveTexture(uint texture);
		private delegate void GlAttachShader(uint program, uint shader);
		private delegate void GlBindFramebuffer(uint target, uint framebuffer);
		private delegate uint GlCheckFramebufferStatus(uint target);
		private delegate void GlCompileShader(uint shader);
		private delegate uint GlCreateProgram();
		private delegate uint GlCreateShader(uint shaderType);
		private delegate void GlDeleteFramebuffers(int n, uint[] framebuffers);
		private delegate void GlDeleteShader(uint shader);
		private delegate void GlFramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level);
		private delegate void GlGenFramebuffers(int n, uint[] framebuffers);
		private delegate void GlGetProgramInfoLog(uint program, int maxLength, out int length, StringBuilder infoLog);
		private delegate void GlGetProgramiv(uint program, uint pname, out int value);
		private delegate void GlGetShaderInfoLog(uint shader, int maxLength, out int length, StringBuilder infoLog);
		private delegate void GlGetShaderiv(uint shader, uint pname, out int value);
		private delegate int GlGetUniformLocation(uint program, string name);
		private delegate void GlLinkProgram(uint program);
		private delegate void GlShaderSource(uint shader, int count, string[] source, int[] length);
		private delegate void GlUniform1i(int location, int value);
		private delegate void GlUniform2f(int location, float value0, float value1);
		private delegate void GlUniform3f(int location, float value0, float value1, float value2);
		private delegate void GlUseProgram(uint program);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("opengl32.dll")]
		private static extern IntPtr wglGetProcAddress(string lpszProc);

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
		private static extern void glTexImage2D(uint target, int level, uint internalFormat, int width, int height, int border, uint format, uint type, IntPtr pixels);

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
