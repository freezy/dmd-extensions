using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using LibDmd.Frame;
using LibDmd.Output.NativeWindow;
using NLog;

namespace LibDmd.Output.NativeWindow
{
	/// <summary>
	/// OpenGL ES 2.0 port of the dmdext virtual-DMD renderer (the dot signed-distance shader plus
	/// the two-stage gaussian glow). Unlike <c>VirtualDmdOpenGlPipeline</c> (desktop GL, fixed-function
	/// immediate mode) this uses a VBO quad + programmable pipeline so it runs on GL ES / ANGLE, which
	/// is required on macOS (desktop GL is deprecated there) and on mobile.
	/// </summary>
	/// <remarks>
	/// All GL calls must happen on the thread that owns the GL context (the host pump thread).
	/// Faithful to the original passes: source(NEAREST) → blur2 H/V → blur12 H/V → composite.
	/// ORIENTATION NOTE: the source upload is row-flipped (as in the desktop renderer) and the final
	/// quad maps uv directly. If the image is upside-down on first Mac run, drop the flip in
	/// <see cref="GetUploadBuffer"/> OR invert uv.y in the vertex shader — a one-line change.
	/// </remarks>
	internal sealed class GlesDmdPipeline : IDisposable
	{
		private readonly Dimensions _size;
		private readonly Func<string, IntPtr> _getProc;
		private DmdWindowStyle _style;
		private bool _ready;
		private bool _dmdProgramInvalid;

		private byte[] _uploadBuffer;
		private uint _quadVbo;
		private uint _sourceTexture;
		private bool _sourceCreated;
		private uint _dotGlowTexture;
		private uint _backGlowTexture;
		private uint _tempTexture;
		private uint _dotGlowFbo;
		private uint _backGlowFbo;
		private uint _tempFbo;
		private bool _fbosCreated;

		private uint _blur2Program;
		private uint _blur12Program;
		private uint _dmdProgram;
		private int _blur2TexU, _blur2DirU, _blur2RectU;
		private int _blur12TexU, _blur12DirU, _blur12RectU;
		private int _dmdTexU, _dmdDotGlowU, _dmdBackGlowU, _dmdSizeU, _dmdUnlitU, _dmdRectU;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public GlesDmdPipeline(Dimensions size, DmdWindowStyle style, Func<string, IntPtr> getProc)
		{
			_size = size;
			_style = style ?? new DmdWindowStyle();
			_getProc = getProc;
		}

		public bool Initialize()
		{
			try {
				LoadFunctions();
				CreateQuad();
				_blur2Program = CreateProgram(VertexShader, BlurPrelude + "void main() { gl_FragColor = vec4(blur_level_2(uTex, uv, uDir).rgb, 1.0); }");
				_blur12Program = CreateProgram(VertexShader, BlurPrelude + "void main() { gl_FragColor = vec4(blur_level_12(uTex, uv, uDir).rgb, 1.0); }");
				_blur2TexU = _getUniform(_blur2Program, "uTex");
				_blur2DirU = _getUniform(_blur2Program, "uDir");
				_blur2RectU = _getUniform(_blur2Program, "uRect");
				_blur12TexU = _getUniform(_blur12Program, "uTex");
				_blur12DirU = _getUniform(_blur12Program, "uDir");
				_blur12RectU = _getUniform(_blur12Program, "uRect");
				_dmdProgramInvalid = true;
				EnsureDmdProgram();
				_ready = true;
				Logger.Info("[DMD] GL ES DMD pipeline initialized.");
				return true;
			} catch (Exception exception) {
				Logger.Error(exception, "[DMD] GL ES DMD pipeline initialization failed.");
				_ready = false;
				return false;
			}
		}

		public void SetStyle(DmdWindowStyle style)
		{
			style = style ?? new DmdWindowStyle();
			if (StyleEquals(_style, style)) {
				return;
			}
			_style = style;
			_dmdProgramInvalid = true;
		}

		public void Render(byte[] rgba, int drawableWidth, int drawableHeight)
		{
			if (!_ready || rgba == null || drawableWidth <= 0 || drawableHeight <= 0) {
				return;
			}

			EnsureDmdProgram();
			EnsureFbos();
			UploadSource(rgba);

			// Glow: two separable blur stages, full DMD-resolution FBOs.
			RunBlur(_blur2Program, _blur2TexU, _blur2DirU, _blur2RectU, _sourceTexture, _tempFbo, 1f / _size.Width, 0f);
			RunBlur(_blur2Program, _blur2TexU, _blur2DirU, _blur2RectU, _tempTexture, _dotGlowFbo, 0f, 1f / _size.Height);
			RunBlur(_blur12Program, _blur12TexU, _blur12DirU, _blur12RectU, _dotGlowTexture, _tempFbo, 1f / _size.Width, 0f);
			RunBlur(_blur12Program, _blur12TexU, _blur12DirU, _blur12RectU, _tempTexture, _backGlowFbo, 0f, 1f / _size.Height);

			// Composite to the default framebuffer, letterboxed to the DMD aspect ratio.
			_glBindFramebuffer(GL_FRAMEBUFFER, 0);
			_glViewport(0, 0, drawableWidth, drawableHeight);
			_glClearColor(0f, 0f, 0f, 1f);
			_glClear(GL_COLOR_BUFFER_BIT);

			_glUseProgram(_dmdProgram);
			BindTexture(0, _sourceTexture);
			BindTexture(1, _dotGlowTexture);
			BindTexture(2, _backGlowTexture);
			_glUniform1i(_dmdTexU, 0);
			_glUniform1i(_dmdDotGlowU, 1);
			_glUniform1i(_dmdBackGlowU, 2);
			_glUniform2f(_dmdSizeU, _size.Width, _size.Height);
			if (_dmdUnlitU != -1) {
				var b = Math.Max(0.0001f, _style.Brightness);
				_glUniform3f(_dmdUnlitU, _style.UnlitDotR / b, _style.UnlitDotG / b, _style.UnlitDotB / b);
			}
			SetRect(_dmdRectU, ComputeLetterbox(drawableWidth, drawableHeight));
			DrawQuad();
			_glUseProgram(0);
		}

		// --- passes -------------------------------------------------------------------------------

		private void RunBlur(uint program, int texU, int dirU, int rectU, uint inputTexture, uint fbo, float dirX, float dirY)
		{
			_glBindFramebuffer(GL_FRAMEBUFFER, fbo);
			_glViewport(0, 0, _size.Width, _size.Height);
			_glClear(GL_COLOR_BUFFER_BIT);
			_glUseProgram(program);
			BindTexture(0, inputTexture);
			_glUniform1i(texU, 0);
			_glUniform2f(dirU, dirX, dirY);
			SetRect(rectU, FullRect);
			DrawQuad();
			_glUseProgram(0);
		}

		private void EnsureDmdProgram()
		{
			if (!_dmdProgramInvalid && _dmdProgram != 0) {
				return;
			}
			if (_dmdProgram != 0) {
				// Style changed: drop the old program before building the replacement, otherwise it
				// leaks for the lifetime of the GL context (one per style reload).
				_glUseProgram(0);
				_glDeleteProgram(_dmdProgram);
				_dmdProgram = 0;
			}
			_dmdProgram = CreateProgram(VertexShader, BuildDmdFragment(_style));
			_dmdTexU = _getUniform(_dmdProgram, "dmdTexture");
			_dmdDotGlowU = _getUniform(_dmdProgram, "dmdDotGlow");
			_dmdBackGlowU = _getUniform(_dmdProgram, "dmdBackGlow");
			_dmdSizeU = _getUniform(_dmdProgram, "dmdSize");
			_dmdUnlitU = _getUniform(_dmdProgram, "unlitDot");
			_dmdRectU = _getUniform(_dmdProgram, "uRect");
			_dmdProgramInvalid = false;
		}

		private void EnsureFbos()
		{
			if (_fbosCreated) {
				return;
			}
			_dotGlowTexture = CreateRenderTexture();
			_backGlowTexture = CreateRenderTexture();
			_tempTexture = CreateRenderTexture();
			_dotGlowFbo = CreateFbo(_dotGlowTexture);
			_backGlowFbo = CreateFbo(_backGlowTexture);
			_tempFbo = CreateFbo(_tempTexture);
			_fbosCreated = true;
		}

		private void UploadSource(byte[] rgba)
		{
			if (_sourceTexture == 0) {
				_sourceTexture = GenTexture();
			}
			var upload = GetUploadBuffer(rgba);
			_glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
			_glActiveTexture(GL_TEXTURE0);
			_glBindTexture(GL_TEXTURE_2D, _sourceTexture);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_NEAREST);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_NEAREST);
			if (_sourceCreated) {
				_glTexSubImage2DBytes(GL_TEXTURE_2D, 0, 0, 0, _size.Width, _size.Height, GL_RGBA, GL_UNSIGNED_BYTE, upload);
			} else {
				_glTexImage2DBytes(GL_TEXTURE_2D, 0, (int)GL_RGBA, _size.Width, _size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, upload);
				_sourceCreated = true;
			}
		}

		private byte[] GetUploadBuffer(byte[] rgba)
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

		private uint CreateRenderTexture()
		{
			var tex = GenTexture();
			_glBindTexture(GL_TEXTURE_2D, tex);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
			_glTexImage2DPtr(GL_TEXTURE_2D, 0, (int)GL_RGBA, _size.Width, _size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
			return tex;
		}

		private uint CreateFbo(uint texture)
		{
			var fbos = new uint[1];
			_glGenFramebuffers(1, fbos);
			_glBindFramebuffer(GL_FRAMEBUFFER, fbos[0]);
			_glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, texture, 0);
			var status = _glCheckFramebufferStatus(GL_FRAMEBUFFER);
			if (status != GL_FRAMEBUFFER_COMPLETE) {
				throw new InvalidOperationException($"GL ES framebuffer incomplete: 0x{status:x}.");
			}
			_glBindFramebuffer(GL_FRAMEBUFFER, 0);
			return fbos[0];
		}

		private uint GenTexture()
		{
			var t = new uint[1];
			_glGenTextures(1, t);
			return t[0];
		}

		private void BindTexture(uint unit, uint texture)
		{
			_glActiveTexture(GL_TEXTURE0 + unit);
			_glBindTexture(GL_TEXTURE_2D, texture);
		}

		// --- quad ---------------------------------------------------------------------------------

		private static readonly float[] QuadData = {
			// pos    uv
			0f, 0f,  0f, 0f,
			1f, 0f,  1f, 0f,
			0f, 1f,  0f, 1f,
			1f, 1f,  1f, 1f,
		};

		private static readonly float[] FullRect = { -1f, -1f, 1f, 1f };

		private void CreateQuad()
		{
			var buffers = new uint[1];
			_glGenBuffers(1, buffers);
			_quadVbo = buffers[0];
			_glBindBuffer(GL_ARRAY_BUFFER, _quadVbo);
			_glBufferData(GL_ARRAY_BUFFER, new IntPtr(QuadData.Length * sizeof(float)), QuadData, GL_STATIC_DRAW);
		}

		private void DrawQuad()
		{
			_glBindBuffer(GL_ARRAY_BUFFER, _quadVbo);
			_glEnableVertexAttribArray(AttribPos);
			_glVertexAttribPointer(AttribPos, 2, GL_FLOAT, false, 4 * sizeof(float), IntPtr.Zero);
			_glEnableVertexAttribArray(AttribUv);
			_glVertexAttribPointer(AttribUv, 2, GL_FLOAT, false, 4 * sizeof(float), new IntPtr(2 * sizeof(float)));
			_glDrawArrays(GL_TRIANGLE_STRIP, 0, 4);
		}

		private void SetRect(int rectUniform, float[] rect)
		{
			if (rectUniform != -1) {
				_glUniform4f(rectUniform, rect[0], rect[1], rect[2], rect[3]);
			}
		}

		// Reused so the per-frame composite doesn't allocate (runs at the host render cadence).
		private readonly float[] _letterboxRect = new float[4];

		private float[] ComputeLetterbox(int w, int h)
		{
			float dmdAspect = (float)_size.Width / _size.Height;
			float winAspect = (float)w / h;
			float drawW, drawH;
			if (winAspect > dmdAspect) {
				drawH = h;
				drawW = h * dmdAspect;
			} else {
				drawW = w;
				drawH = w / dmdAspect;
			}
			float left = (w - drawW) * 0.5f;
			float top = (h - drawH) * 0.5f;
			float right = left + drawW;
			float bottom = top + drawH;
			// pixel (y down) -> NDC (y up); rect = (x0, yBottom, x1, yTop) to match quad pos.y: 0=bottom,1=top.
			float Nx(float px) => px / w * 2f - 1f;
			float Ny(float py) => 1f - py / h * 2f;
			_letterboxRect[0] = Nx(left);
			_letterboxRect[1] = Ny(bottom);
			_letterboxRect[2] = Nx(right);
			_letterboxRect[3] = Ny(top);
			return _letterboxRect;
		}

		// --- shader build -------------------------------------------------------------------------

		private string BuildDmdFragment(DmdWindowStyle style)
		{
			var nfi = NumberFormatInfo.InvariantInfo;
			var b = new StringBuilder();
			b.AppendLine("#version 100");
			b.AppendLine("precision highp float;");
			if (HasBackGlow(style)) b.AppendLine("#define BACKGLOW");
			if (HasDotGlow(style)) b.AppendLine("#define DOTGLOW");
			if (HasBrightness(style)) b.AppendLine("#define BRIGHTNESS");
			if (HasUnlitDot(style)) b.AppendLine("#define UNLIT");
			if (HasGamma(style)) b.AppendLine("#define GAMMA");
			if (style.DotSize > 0.5f) b.AppendLine("#define DOT_OVERLAP");
			b.AppendFormat(nfi, "const float dotSize = {0:0.00000};\n", style.DotSize);
			b.AppendFormat(nfi, "const float dotRounding = {0:0.00000};\n", style.DotRounding);
			b.AppendFormat(nfi, "const float sharpMax = {0:0.00000};\n", 0.01f + style.DotSize * (1.0f - style.DotSharpness));
			b.AppendFormat(nfi, "const float sharpMin = {0:0.00000};\n", -0.01f - style.DotSize * (1.0f - style.DotSharpness));
			b.AppendFormat(nfi, "const float brightness = {0:0.00000};\n", style.Brightness);
			b.AppendFormat(nfi, "const float backGlow = {0:0.00000};\n", style.BackGlow);
			b.AppendFormat(nfi, "const float dotGlow = {0:0.00000};\n", style.DotGlow);
			b.AppendFormat(nfi, "const float gamma = {0:0.00000};\n", style.Gamma);
			b.Append(DmdFragmentBody);
			return b.ToString();
		}

		private static bool HasUnlitDot(DmdWindowStyle s) => s.UnlitDotR > 0f || s.UnlitDotG > 0f || s.UnlitDotB > 0f;
		private static bool HasBrightness(DmdWindowStyle s) => Math.Abs(s.Brightness - 1.0f) > 0.01f;
		private static bool HasDotGlow(DmdWindowStyle s) => s.DotGlow > 0.01f;
		private static bool HasBackGlow(DmdWindowStyle s) => s.BackGlow > 0.01f;
		private static bool HasGamma(DmdWindowStyle s) => Math.Abs(s.Gamma - 1.0f) > 0.01f;

		private static bool StyleEquals(DmdWindowStyle a, DmdWindowStyle b)
		{
			return a.DotSize == b.DotSize && a.DotRounding == b.DotRounding && a.DotSharpness == b.DotSharpness
				&& a.UnlitDotR == b.UnlitDotR && a.UnlitDotG == b.UnlitDotG && a.UnlitDotB == b.UnlitDotB
				&& a.Brightness == b.Brightness && a.DotGlow == b.DotGlow && a.BackGlow == b.BackGlow
				&& a.Gamma == b.Gamma && a.GlassR == b.GlassR && a.GlassG == b.GlassG && a.GlassB == b.GlassB
				&& a.GlassLighting == b.GlassLighting;
		}

		private const uint AttribPos = 0;
		private const uint AttribUv = 1;

		private const string VertexShader = @"#version 100
attribute vec2 aPos;
attribute vec2 aUv;
varying vec2 uv;
uniform vec4 uRect; // x0, yBottom, x1, yTop (NDC)
void main() {
	uv = aUv;
	vec2 ndc = mix(uRect.xy, uRect.zw, aPos);
	gl_Position = vec4(ndc, 0.0, 1.0);
}";

		private const string BlurPrelude = @"#version 100
precision highp float;
varying vec2 uv;
uniform sampler2D uTex;
uniform vec2 uDir;
vec4 blur_level_2(sampler2D image, vec2 coord, vec2 dir) {
	vec4 color = vec4(0.0);
	color += texture2D(image, coord) * 0.5;
	color += texture2D(image, coord + dir) * 0.25;
	color += texture2D(image, coord - dir) * 0.25;
	return color;
}
vec4 blur_level_12(sampler2D image, vec2 coord, vec2 dir) {
	vec4 color = vec4(0.0);
	vec2 off1 = vec2(1.3846153846) * dir;
	vec2 off2 = vec2(3.2307692308) * dir;
	color += texture2D(image, coord) * 0.2270270270;
	color += texture2D(image, coord + off1) * 0.3162162162;
	color += texture2D(image, coord - off1) * 0.3162162162;
	color += texture2D(image, coord + off2) * 0.0702702703;
	color += texture2D(image, coord - off2) * 0.0702702703;
	return color;
}
";

		private const string DmdFragmentBody = @"
varying vec2 uv;
uniform sampler2D dmdTexture;
uniform sampler2D dmdDotGlow;
uniform sampler2D dmdBackGlow;
uniform vec2 dmdSize;
uniform vec3 unlitDot;

float udRoundBox(vec2 p, float b, float r) {
	vec2 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

vec3 computeDotColor(vec2 ofs) {
	vec2 nearest = (floor(uv * dmdSize) + ofs) / dmdSize;
	vec2 pos = 2.0 * (fract(uv * dmdSize) - ofs);
	float dotv = smoothstep(sharpMax, sharpMin, udRoundBox(pos, dotSize, dotRounding * dotSize));
	vec3 dmd = texture2D(dmdTexture, nearest).rgb + unlitDot;
#ifndef UNLIT
	dmd -= unlitDot;
#endif
	return dmd * dotv;
}

void main() {
#ifdef DOT_OVERLAP
	vec3 dotColor = vec3(0.0);
	for (int x = -1; x <= 1; x++) {
		for (int y = -1; y <= 1; y++) {
			dotColor = max(dotColor, computeDotColor(vec2(float(x) + 0.5, float(y) + 0.5)));
		}
	}
#else
	vec3 dotColor = computeDotColor(vec2(0.5, 0.5));
#endif
#ifdef DOTGLOW
	dotColor += texture2D(dmdDotGlow, uv).rgb * dotGlow;
#endif
#ifdef BACKGLOW
	dotColor += texture2D(dmdBackGlow, uv).rgb * backGlow;
#endif
#ifdef BRIGHTNESS
	dotColor *= brightness;
#endif
#ifdef GAMMA
	dotColor = pow(dotColor, vec3(1.0 / gamma));
#endif
	gl_FragColor = vec4(dotColor, 1.0);
}";

		// --- GL program helpers -------------------------------------------------------------------

		private uint CreateProgram(string vertexSource, string fragmentSource)
		{
			var vs = CompileShader(GL_VERTEX_SHADER, vertexSource);
			var fs = CompileShader(GL_FRAGMENT_SHADER, fragmentSource);
			var program = _glCreateProgram();
			_glAttachShader(program, vs);
			_glAttachShader(program, fs);
			_glBindAttribLocation(program, AttribPos, "aPos");
			_glBindAttribLocation(program, AttribUv, "aUv");
			_glLinkProgram(program);
			_glGetProgramiv(program, GL_LINK_STATUS, out var status);
			if (status == 0) {
				throw new InvalidOperationException("GL ES program link failed: " + GetProgramLog(program));
			}
			_glDeleteShader(vs);
			_glDeleteShader(fs);
			return program;
		}

		private uint CompileShader(uint type, string source)
		{
			var shader = _glCreateShader(type);
			_glShaderSource(shader, 1, new[] { source }, new[] { source.Length });
			_glCompileShader(shader);
			_glGetShaderiv(shader, GL_COMPILE_STATUS, out var status);
			if (status == 0) {
				throw new InvalidOperationException("GL ES shader compile failed: " + GetShaderLog(shader));
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

		private int _getUniform(uint program, string name) => _glGetUniformLocation(program, name);

		public void Dispose()
		{
			if (!_ready) {
				return;
			}
			_ready = false;
			_glUseProgram(0);

			if (_fbosCreated) {
				_glDeleteFramebuffers(3, new[] { _dotGlowFbo, _backGlowFbo, _tempFbo });
				_glDeleteTextures(3, new[] { _dotGlowTexture, _backGlowTexture, _tempTexture });
				_dotGlowFbo = _backGlowFbo = _tempFbo = 0;
				_dotGlowTexture = _backGlowTexture = _tempTexture = 0;
				_fbosCreated = false;
			}

			if (_sourceTexture != 0) {
				_glDeleteTextures(1, new[] { _sourceTexture });
				_sourceTexture = 0;
				_sourceCreated = false;
			}

			DeleteProgram(ref _blur2Program);
			DeleteProgram(ref _blur12Program);
			DeleteProgram(ref _dmdProgram);

			if (_quadVbo != 0) {
				_glDeleteBuffers(1, new[] { _quadVbo });
				_quadVbo = 0;
			}
		}

		private void DeleteProgram(ref uint program)
		{
			if (program != 0) {
				_glDeleteProgram(program);
				program = 0;
			}
		}

		// --- GL ES function loading ---------------------------------------------------------------

		private void LoadFunctions()
		{
			_glGenTextures = Load<GlGenTextures>("glGenTextures");
			_glBindTexture = Load<GlBindTexture>("glBindTexture");
			_glTexParameteri = Load<GlTexParameteri>("glTexParameteri");
			_glTexImage2DBytes = Load<GlTexImage2DBytes>("glTexImage2D");
			_glTexImage2DPtr = Load<GlTexImage2DPtr>("glTexImage2D");
			_glTexSubImage2DBytes = Load<GlTexSubImage2DBytes>("glTexSubImage2D");
			_glPixelStorei = Load<GlPixelStorei>("glPixelStorei");
			_glActiveTexture = Load<GlActiveTexture>("glActiveTexture");
			_glGenFramebuffers = Load<GlGenFramebuffers>("glGenFramebuffers");
			_glBindFramebuffer = Load<GlBindFramebuffer>("glBindFramebuffer");
			_glFramebufferTexture2D = Load<GlFramebufferTexture2D>("glFramebufferTexture2D");
			_glCheckFramebufferStatus = Load<GlCheckFramebufferStatus>("glCheckFramebufferStatus");
			_glDeleteFramebuffers = Load<GlDeleteFramebuffers>("glDeleteFramebuffers");
			_glGenBuffers = Load<GlGenBuffers>("glGenBuffers");
			_glBindBuffer = Load<GlBindBuffer>("glBindBuffer");
			_glBufferData = Load<GlBufferData>("glBufferData");
			_glEnableVertexAttribArray = Load<GlEnableVertexAttribArray>("glEnableVertexAttribArray");
			_glVertexAttribPointer = Load<GlVertexAttribPointer>("glVertexAttribPointer");
			_glBindAttribLocation = Load<GlBindAttribLocation>("glBindAttribLocation");
			_glDrawArrays = Load<GlDrawArrays>("glDrawArrays");
			_glCreateShader = Load<GlCreateShader>("glCreateShader");
			_glShaderSource = Load<GlShaderSource>("glShaderSource");
			_glCompileShader = Load<GlCompileShader>("glCompileShader");
			_glGetShaderiv = Load<GlGetShaderiv>("glGetShaderiv");
			_glGetShaderInfoLog = Load<GlGetShaderInfoLog>("glGetShaderInfoLog");
			_glCreateProgram = Load<GlCreateProgram>("glCreateProgram");
			_glAttachShader = Load<GlAttachShader>("glAttachShader");
			_glLinkProgram = Load<GlLinkProgram>("glLinkProgram");
			_glGetProgramiv = Load<GlGetProgramiv>("glGetProgramiv");
			_glGetProgramInfoLog = Load<GlGetProgramInfoLog>("glGetProgramInfoLog");
			_glDeleteShader = Load<GlDeleteShader>("glDeleteShader");
			_glUseProgram = Load<GlUseProgram>("glUseProgram");
			_glGetUniformLocation = Load<GlGetUniformLocation>("glGetUniformLocation");
			_glUniform1i = Load<GlUniform1i>("glUniform1i");
			_glUniform2f = Load<GlUniform2f>("glUniform2f");
			_glUniform3f = Load<GlUniform3f>("glUniform3f");
			_glUniform4f = Load<GlUniform4f>("glUniform4f");
			_glViewport = Load<GlViewport>("glViewport");
			_glClear = Load<GlClear>("glClear");
			_glClearColor = Load<GlClearColor>("glClearColor");
			_glDeleteProgram = Load<GlDeleteProgram>("glDeleteProgram");
			_glDeleteTextures = Load<GlDeleteTextures>("glDeleteTextures");
			_glDeleteBuffers = Load<GlDeleteBuffers>("glDeleteBuffers");
		}

		private T Load<T>(string name) where T : Delegate
		{
			var addr = _getProc?.Invoke(name) ?? IntPtr.Zero;
			if (addr == IntPtr.Zero) {
				throw new InvalidOperationException($"GL ES function {name} is unavailable.");
			}
			return Marshal.GetDelegateForFunctionPointer<T>(addr);
		}

		// --- GL constants -------------------------------------------------------------------------

		private const uint GL_COLOR_ATTACHMENT0 = 0x8CE0;
		private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
		private const uint GL_COMPILE_STATUS = 0x8B81;
		private const uint GL_CLAMP_TO_EDGE = 0x812F;
		private const uint GL_FRAGMENT_SHADER = 0x8B30;
		private const uint GL_FRAMEBUFFER = 0x8D40;
		private const uint GL_FRAMEBUFFER_COMPLETE = 0x8CD5;
		private const uint GL_INFO_LOG_LENGTH = 0x8B84;
		private const uint GL_LINEAR = 0x2601;
		private const uint GL_LINK_STATUS = 0x8B82;
		private const uint GL_NEAREST = 0x2600;
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
		private const uint GL_ARRAY_BUFFER = 0x8892;
		private const uint GL_STATIC_DRAW = 0x88E4;
		private const uint GL_FLOAT = 0x1406;
		private const uint GL_TRIANGLE_STRIP = 0x0005;

		// --- GL ES delegates ----------------------------------------------------------------------

		private delegate void GlGenTextures(int n, uint[] textures);
		private delegate void GlBindTexture(uint target, uint texture);
		private delegate void GlTexParameteri(uint target, uint pname, int param);
		private delegate void GlTexImage2DBytes(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, byte[] pixels);
		private delegate void GlTexImage2DPtr(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, IntPtr pixels);
		private delegate void GlTexSubImage2DBytes(uint target, int level, int xoffset, int yoffset, int width, int height, uint format, uint type, byte[] pixels);
		private delegate void GlPixelStorei(uint pname, int param);
		private delegate void GlActiveTexture(uint texture);
		private delegate void GlGenFramebuffers(int n, uint[] framebuffers);
		private delegate void GlBindFramebuffer(uint target, uint framebuffer);
		private delegate void GlFramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level);
		private delegate uint GlCheckFramebufferStatus(uint target);
		private delegate void GlDeleteFramebuffers(int n, uint[] framebuffers);
		private delegate void GlGenBuffers(int n, uint[] buffers);
		private delegate void GlBindBuffer(uint target, uint buffer);
		private delegate void GlBufferData(uint target, IntPtr size, float[] data, uint usage);
		private delegate void GlEnableVertexAttribArray(uint index);
		private delegate void GlVertexAttribPointer(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer);
		private delegate void GlBindAttribLocation(uint program, uint index, string name);
		private delegate void GlDrawArrays(uint mode, int first, int count);
		private delegate uint GlCreateShader(uint type);
		private delegate void GlShaderSource(uint shader, int count, string[] source, int[] length);
		private delegate void GlCompileShader(uint shader);
		private delegate void GlGetShaderiv(uint shader, uint pname, out int value);
		private delegate void GlGetShaderInfoLog(uint shader, int maxLength, out int length, StringBuilder infoLog);
		private delegate uint GlCreateProgram();
		private delegate void GlAttachShader(uint program, uint shader);
		private delegate void GlLinkProgram(uint program);
		private delegate void GlGetProgramiv(uint program, uint pname, out int value);
		private delegate void GlGetProgramInfoLog(uint program, int maxLength, out int length, StringBuilder infoLog);
		private delegate void GlDeleteShader(uint shader);
		private delegate void GlUseProgram(uint program);
		private delegate int GlGetUniformLocation(uint program, string name);
		private delegate void GlUniform1i(int location, int v0);
		private delegate void GlUniform2f(int location, float v0, float v1);
		private delegate void GlUniform3f(int location, float v0, float v1, float v2);
		private delegate void GlUniform4f(int location, float v0, float v1, float v2, float v3);
		private delegate void GlViewport(int x, int y, int width, int height);
		private delegate void GlClear(uint mask);
		private delegate void GlClearColor(float r, float g, float b, float a);
		private delegate void GlDeleteProgram(uint program);
		private delegate void GlDeleteTextures(int n, uint[] textures);
		private delegate void GlDeleteBuffers(int n, uint[] buffers);

		private GlGenTextures _glGenTextures;
		private GlBindTexture _glBindTexture;
		private GlTexParameteri _glTexParameteri;
		private GlTexImage2DBytes _glTexImage2DBytes;
		private GlTexImage2DPtr _glTexImage2DPtr;
		private GlTexSubImage2DBytes _glTexSubImage2DBytes;
		private GlPixelStorei _glPixelStorei;
		private GlActiveTexture _glActiveTexture;
		private GlGenFramebuffers _glGenFramebuffers;
		private GlBindFramebuffer _glBindFramebuffer;
		private GlFramebufferTexture2D _glFramebufferTexture2D;
		private GlCheckFramebufferStatus _glCheckFramebufferStatus;
		private GlDeleteFramebuffers _glDeleteFramebuffers;
		private GlGenBuffers _glGenBuffers;
		private GlBindBuffer _glBindBuffer;
		private GlBufferData _glBufferData;
		private GlEnableVertexAttribArray _glEnableVertexAttribArray;
		private GlVertexAttribPointer _glVertexAttribPointer;
		private GlBindAttribLocation _glBindAttribLocation;
		private GlDrawArrays _glDrawArrays;
		private GlCreateShader _glCreateShader;
		private GlShaderSource _glShaderSource;
		private GlCompileShader _glCompileShader;
		private GlGetShaderiv _glGetShaderiv;
		private GlGetShaderInfoLog _glGetShaderInfoLog;
		private GlCreateProgram _glCreateProgram;
		private GlAttachShader _glAttachShader;
		private GlLinkProgram _glLinkProgram;
		private GlGetProgramiv _glGetProgramiv;
		private GlGetProgramInfoLog _glGetProgramInfoLog;
		private GlDeleteShader _glDeleteShader;
		private GlUseProgram _glUseProgram;
		private GlGetUniformLocation _glGetUniformLocation;
		private GlUniform1i _glUniform1i;
		private GlUniform2f _glUniform2f;
		private GlUniform3f _glUniform3f;
		private GlUniform4f _glUniform4f;
		private GlViewport _glViewport;
		private GlClear _glClear;
		private GlClearColor _glClearColor;
		private GlDeleteProgram _glDeleteProgram;
		private GlDeleteTextures _glDeleteTextures;
		private GlDeleteBuffers _glDeleteBuffers;
	}
}
