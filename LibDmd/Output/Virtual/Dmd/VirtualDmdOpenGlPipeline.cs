using System;
using System.Runtime.InteropServices;
using System.Text;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.Virtual.Dmd
{
	public sealed class VirtualDmdRenderStyle : IEquatable<VirtualDmdRenderStyle>
	{
		public static readonly VirtualDmdRenderStyle Default = new VirtualDmdRenderStyle();

		public float DotSize { get; set; } = 0.92f;
		public float DotRounding { get; set; } = 1.0f;
		public float DotSharpness { get; set; } = 0.8f;
		public float UnlitDotR { get; set; }
		public float UnlitDotG { get; set; }
		public float UnlitDotB { get; set; }
		public float Brightness { get; set; } = 0.95f;
		public float DotGlow { get; set; }
		public float BackGlow { get; set; }
		public float Gamma { get; set; } = 1.0f;
		public float GlassR { get; set; }
		public float GlassG { get; set; }
		public float GlassB { get; set; }
		public float GlassLighting { get; set; }

		public bool HasUnlitDot => UnlitDotR > 0f || UnlitDotG > 0f || UnlitDotB > 0f;
		public bool HasBrightness => Math.Abs(Brightness - 1.0f) > 0.01f;
		public bool HasDotGlow => DotGlow > 0.01f;
		public bool HasBackGlow => BackGlow > 0.01f;
		public bool HasGamma => Math.Abs(Gamma - 1.0f) > 0.01f;
		public bool HasGlass => GlassLighting > 0.0f || GlassR > 0f || GlassG > 0f || GlassB > 0f;

		public bool Equals(VirtualDmdRenderStyle other)
		{
			if (other == null) {
				return false;
			}

			return DotSize.Equals(other.DotSize)
				&& DotRounding.Equals(other.DotRounding)
				&& DotSharpness.Equals(other.DotSharpness)
				&& UnlitDotR.Equals(other.UnlitDotR)
				&& UnlitDotG.Equals(other.UnlitDotG)
				&& UnlitDotB.Equals(other.UnlitDotB)
				&& Brightness.Equals(other.Brightness)
				&& DotGlow.Equals(other.DotGlow)
				&& BackGlow.Equals(other.BackGlow)
				&& Gamma.Equals(other.Gamma)
				&& GlassR.Equals(other.GlassR)
				&& GlassG.Equals(other.GlassG)
				&& GlassB.Equals(other.GlassB)
				&& GlassLighting.Equals(other.GlassLighting);
		}
	}

	public sealed class VirtualDmdOpenGlPipeline : IDisposable
	{
		private const byte DefaultAlpha = 255;
		private readonly Dimensions _size;
		private readonly Func<string, IntPtr> _loadGlFunction;
		private VirtualDmdRenderStyle _style;
		private bool _sourceTextureCreated;
		private bool _glassTextureCreated;
		private bool _framebuffersCreated;
		private bool _glFunctionsAvailable;
		private bool _dmdRendererAvailable;
		private bool _dmdProgramInvalid;
		private byte[] _uploadBuffer;
		private uint _sourceTexture;
		private uint _glassTexture;
		private uint _dotGlowTexture;
		private uint _backGlowTexture;
		private uint _tempTexture;
		private byte[] _glassRgba;
		private int _glassWidth;
		private int _glassHeight;
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
		private int _dmdGlassTextureUniform;
		private int _dmdGlassTexOffsetUniform;
		private int _dmdGlassTexScaleUniform;
		private int _dmdGlassColorUniform;

		private GlBegin _glBegin;
		private GlBindTexture _glBindTexture;
		private GlClear _glClear;
		private GlClearColor _glClearColor;
		private GlColor3f _glColor3f;
		private GlDisable _glDisable;
		private GlEnable _glEnable;
		private GlEnd _glEnd;
		private GlGenTextures _glGenTextures;
		private GlLoadIdentity _glLoadIdentity;
		private GlMatrixMode _glMatrixMode;
		private GlOrtho _glOrtho;
		private GlPixelStorei _glPixelStorei;
		private GlTexCoord2f _glTexCoord2f;
		private GlTexImage2DIntPtr _glTexImage2DIntPtr;
		private GlTexImage2DBytes _glTexImage2DBytes;
		private GlTexParameteri _glTexParameteri;
		private GlTexSubImage2D _glTexSubImage2D;
		private GlVertex2f _glVertex2f;
		private GlViewport _glViewport;
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
		private GlUniform4f _glUniform4f;
		private GlUseProgram _glUseProgram;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdOpenGlPipeline(Dimensions size)
			: this(size, VirtualDmdRenderStyle.Default)
		{
		}

		public VirtualDmdOpenGlPipeline(Dimensions size, VirtualDmdRenderStyle style)
			: this(size, style, null)
		{
		}

		public VirtualDmdOpenGlPipeline(Dimensions size, VirtualDmdRenderStyle style, Func<string, IntPtr> loadGlFunction)
		{
			_size = size;
			_style = style ?? VirtualDmdRenderStyle.Default;
			_loadGlFunction = loadGlFunction;
			try {
				LoadRendererFunctions();
				_glFunctionsAvailable = true;
				_blur2Program = CreateProgram(PassthroughVertexShader, DesktopHeader + DmdShaderSource.BlurFragmentFunctions + DmdShaderSource.BlurMain2);
				_blur12Program = CreateProgram(PassthroughVertexShader, DesktopHeader + DmdShaderSource.BlurFragmentFunctions + DmdShaderSource.BlurMain12);

				_blur2TextureUniform = _glGetUniformLocation(_blur2Program, "texture");
				_blur2DirectionUniform = _glGetUniformLocation(_blur2Program, "direction");
				_blur12TextureUniform = _glGetUniformLocation(_blur12Program, "texture");
				_blur12DirectionUniform = _glGetUniformLocation(_blur12Program, "direction");
				_dmdProgramInvalid = true;
				EnsureDmdProgram();

				_dmdRendererAvailable = true;
				Logger.Info("[DMD] Shared dmdext OpenGL renderer initialized.");
			} catch (Exception exception) {
				_glFunctionsAvailable = _glViewport != null;
				_dmdRendererAvailable = false;
				Logger.Warn(exception, "[DMD] Shared dmdext OpenGL renderer unavailable; using immediate dot renderer.");
			}
		}

		public void Render(byte[] rgba, int clientWidth, int clientHeight, float offsetX, float offsetY, float renderWidth, float renderHeight)
		{
			if (!_glFunctionsAvailable) {
				return;
			}

			PrepareViewport(clientWidth, clientHeight);
			if (_dmdRendererAvailable) {
				RenderDmdExtPipeline(rgba, clientWidth, clientHeight, offsetX, offsetY, renderWidth, renderHeight);
			} else {
				var scale = renderWidth / _size.Width;
				RenderImmediate(rgba, scale, offsetX, offsetY);
			}
		}

		public void SetStyle(VirtualDmdRenderStyle style)
		{
			style = style ?? VirtualDmdRenderStyle.Default;
			if (_style.Equals(style)) {
				return;
			}

			_style = style;
			_dmdProgramInvalid = true;
		}

		public void SetGlassTexture(byte[] rgba, int width, int height)
		{
			if (rgba == null || width <= 0 || height <= 0) {
				ClearGlassTexture();
				return;
			}

			if (_glassRgba == null || _glassRgba.Length != rgba.Length) {
				_glassRgba = new byte[rgba.Length];
			}

			Buffer.BlockCopy(rgba, 0, _glassRgba, 0, rgba.Length);
			_glassWidth = width;
			_glassHeight = height;
			_glassTextureCreated = false;
			_dmdProgramInvalid = true;
		}

		public void ClearGlassTexture()
		{
			_glassRgba = null;
			_glassWidth = 0;
			_glassHeight = 0;
			_glassTextureCreated = false;
			_dmdProgramInvalid = true;
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
			_glViewport(0, 0, clientWidth, clientHeight);
			_glClearColor(0f, 0f, 0f, 1f);
			_glClear(GL_COLOR_BUFFER_BIT);
			SetOrthographicProjection(clientWidth, clientHeight);
			_glDisable(GL_DEPTH_TEST);
			_glDisable(GL_TEXTURE_2D);
		}

		private void RenderDmdExtPipeline(byte[] rgba, int clientWidth, int clientHeight, float offsetX, float offsetY, float renderWidth, float renderHeight)
		{
			EnsureDmdProgram();
			UploadSourceTexture(rgba);
			UploadGlassTexture();
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
			if (_glassTextureCreated) {
				BindTextureUnit(3, _glassTexture);
			}
			_glUniform1i(_dmdTextureUniform, 0);
			_glUniform1i(_dmdDotGlowUniform, 1);
			_glUniform1i(_dmdBackGlowUniform, 2);
			_glUniform2f(_dmdSizeUniform, _size.Width, _size.Height);
			if (_dmdUnlitDotUniform != -1) {
				_glUniform3f(_dmdUnlitDotUniform, _style.UnlitDotR / Math.Max(0.0001f, _style.Brightness), _style.UnlitDotG / Math.Max(0.0001f, _style.Brightness), _style.UnlitDotB / Math.Max(0.0001f, _style.Brightness));
			}
			if (_dmdGlassTextureUniform != -1) {
				_glUniform1i(_dmdGlassTextureUniform, 3);
			}
			if (_dmdGlassTexOffsetUniform != -1) {
				_glUniform2f(_dmdGlassTexOffsetUniform, 0f, 0f);
			}
			if (_dmdGlassTexScaleUniform != -1) {
				_glUniform2f(_dmdGlassTexScaleUniform, 1f, 1f);
			}
			if (_dmdGlassColorUniform != -1) {
				_glUniform4f(_dmdGlassColorUniform, _style.GlassR, _style.GlassG, _style.GlassB, _style.GlassLighting);
			}
			DrawDmdQuad(offsetX, offsetY, renderWidth, renderHeight);

			_glUseProgram(0);
			_glActiveTexture(GL_TEXTURE0);
			_glDisable(GL_TEXTURE_2D);
		}

		private void EnsureDmdProgram()
		{
			if (!_dmdProgramInvalid && _dmdProgram != 0) {
				return;
			}

			if (_dmdProgram != 0) {
				_glUseProgram(0);
			}

			_dmdProgram = CreateProgram(PassthroughVertexShader, BuildDmdFragmentShader(_style));
			_dmdTextureUniform = _glGetUniformLocation(_dmdProgram, "dmdTexture");
			_dmdDotGlowUniform = _glGetUniformLocation(_dmdProgram, "dmdDotGlow");
			_dmdBackGlowUniform = _glGetUniformLocation(_dmdProgram, "dmdBackGlow");
			_dmdSizeUniform = _glGetUniformLocation(_dmdProgram, "dmdSize");
			_dmdUnlitDotUniform = _glGetUniformLocation(_dmdProgram, "unlitDot");
			_dmdGlassTextureUniform = _glGetUniformLocation(_dmdProgram, "glassTexture");
			_dmdGlassTexOffsetUniform = _glGetUniformLocation(_dmdProgram, "glassTexOffset");
			_dmdGlassTexScaleUniform = _glGetUniformLocation(_dmdProgram, "glassTexScale");
			_dmdGlassColorUniform = _glGetUniformLocation(_dmdProgram, "glassColor");
			_dmdProgramInvalid = false;
		}

		private void RunBlurPass(uint program, int textureUniform, int directionUniform, uint inputTexture, uint framebuffer, float directionX, float directionY)
		{
			_glBindFramebuffer(GL_FRAMEBUFFER, framebuffer);
			SetOrthographicProjection(_size.Width, _size.Height);
			_glClear(GL_COLOR_BUFFER_BIT);
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
			_glGenTextures(1, textures);
			_glBindTexture(GL_TEXTURE_2D, textures[0]);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
			_glTexImage2DIntPtr(GL_TEXTURE_2D, 0, GL_RGBA, _size.Width, _size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
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
				_glGenTextures(1, textures);
				_sourceTexture = textures[0];
			}

			var upload = GetOpenGlUploadBuffer(rgba);
			_glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
			_glBindTexture(GL_TEXTURE_2D, _sourceTexture);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_NEAREST);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_NEAREST);

			if (_sourceTextureCreated) {
				_glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, _size.Width, _size.Height, GL_RGBA, GL_UNSIGNED_BYTE, upload);
			} else {
				_glTexImage2DBytes(GL_TEXTURE_2D, 0, GL_RGBA, _size.Width, _size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, upload);
				_sourceTextureCreated = true;
			}
		}

		private void UploadGlassTexture()
		{
			if (_glassRgba == null || _glassWidth <= 0 || _glassHeight <= 0) {
				return;
			}

			if (_glassTexture == 0) {
				var textures = new uint[1];
				_glGenTextures(1, textures);
				_glassTexture = textures[0];
			}

			_glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
			_glBindTexture(GL_TEXTURE_2D, _glassTexture);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
			_glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

			if (_glassTextureCreated) {
				_glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, _glassWidth, _glassHeight, GL_RGBA, GL_UNSIGNED_BYTE, _glassRgba);
			} else {
				_glTexImage2DBytes(GL_TEXTURE_2D, 0, GL_RGBA, _glassWidth, _glassHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE, _glassRgba);
				_glassTextureCreated = true;
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
			_glEnable(GL_TEXTURE_2D);
			_glBindTexture(GL_TEXTURE_2D, texture);
		}

		private void DrawDmdQuad(float x, float y, float width, float height)
		{
			_glBegin(GL_QUADS);
			_glTexCoord2f(0f, 1f);
			_glVertex2f(x, y);
			_glTexCoord2f(0f, 0f);
			_glVertex2f(x, y + height);
			_glTexCoord2f(1f, 0f);
			_glVertex2f(x + width, y + height);
			_glTexCoord2f(1f, 1f);
			_glVertex2f(x + width, y);
			_glEnd();
		}

		private void SetOrthographicProjection(int width, int height)
		{
			_glViewport(0, 0, width, height);
			_glMatrixMode(GL_PROJECTION);
			_glLoadIdentity();
			_glOrtho(0, width, height, 0, -1, 1);
			_glMatrixMode(GL_MODELVIEW);
			_glLoadIdentity();
		}

		private void RenderImmediate(byte[] rgba, float scale, float offsetX, float offsetY)
		{
			_glDisable(GL_TEXTURE_2D);
			var dotSize = Math.Max(1f, scale * 0.72f);
			var dotPad = (scale - dotSize) * 0.5f;
			var unlitR = 0.028f;
			var unlitG = 0.008f;
			var unlitB = 0f;

			_glBegin(GL_QUADS);
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
			_glEnd();
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
			_glBegin = LoadGlFunction<GlBegin>("glBegin");
			_glBindTexture = LoadGlFunction<GlBindTexture>("glBindTexture");
			_glClear = LoadGlFunction<GlClear>("glClear");
			_glClearColor = LoadGlFunction<GlClearColor>("glClearColor");
			_glColor3f = LoadGlFunction<GlColor3f>("glColor3f");
			_glDisable = LoadGlFunction<GlDisable>("glDisable");
			_glEnable = LoadGlFunction<GlEnable>("glEnable");
			_glEnd = LoadGlFunction<GlEnd>("glEnd");
			_glGenTextures = LoadGlFunction<GlGenTextures>("glGenTextures");
			_glLoadIdentity = LoadGlFunction<GlLoadIdentity>("glLoadIdentity");
			_glMatrixMode = LoadGlFunction<GlMatrixMode>("glMatrixMode");
			_glOrtho = LoadGlFunction<GlOrtho>("glOrtho");
			_glPixelStorei = LoadGlFunction<GlPixelStorei>("glPixelStorei");
			_glTexCoord2f = LoadGlFunction<GlTexCoord2f>("glTexCoord2f");
			_glTexImage2DIntPtr = LoadGlFunction<GlTexImage2DIntPtr>("glTexImage2D");
			_glTexImage2DBytes = LoadGlFunction<GlTexImage2DBytes>("glTexImage2D");
			_glTexParameteri = LoadGlFunction<GlTexParameteri>("glTexParameteri");
			_glTexSubImage2D = LoadGlFunction<GlTexSubImage2D>("glTexSubImage2D");
			_glVertex2f = LoadGlFunction<GlVertex2f>("glVertex2f");
			_glViewport = LoadGlFunction<GlViewport>("glViewport");
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
			_glUniform4f = LoadGlFunction<GlUniform4f>("glUniform4f");
			_glUseProgram = LoadGlFunction<GlUseProgram>("glUseProgram");
		}

		private T LoadGlFunction<T>(string name) where T : Delegate
		{
			var address = _loadGlFunction?.Invoke(name) ?? IntPtr.Zero;
			if (_loadGlFunction != null) {
				if (IsInvalidGlAddress(address)) {
					throw new InvalidOperationException($"OpenGL function {name} is unavailable.");
				}
				return Marshal.GetDelegateForFunctionPointer<T>(address);
			}

			if (IsInvalidGlAddress(address)) {
				address = wglGetProcAddress(name);
			}
			if (IsInvalidGlAddress(address)) {
				var module = LoadLibrary("opengl32.dll");
				address = GetProcAddress(module, name);
			}
			if (IsInvalidGlAddress(address)) {
				throw new InvalidOperationException($"OpenGL function {name} is unavailable.");
			}
			return Marshal.GetDelegateForFunctionPointer<T>(address);
		}

		private static bool IsInvalidGlAddress(IntPtr address)
		{
			return address == IntPtr.Zero || address == new IntPtr(1) || address == new IntPtr(2) || address == new IntPtr(3) || address == new IntPtr(-1);
		}

		private void DrawDot(float x, float y, float size, float r, float g, float b)
		{
			_glColor3f(r, g, b);
			_glVertex2f(x, y);
			_glVertex2f(x + size, y);
			_glVertex2f(x + size, y + size);
			_glVertex2f(x, y + size);
		}

		private const string PassthroughVertexShader = @"
#version 120
varying vec2 uv;

void main()
{
	uv = gl_MultiTexCoord0.xy;
	gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
}";

		private const string DesktopHeader = "#version 120\n";

		private string BuildDmdFragmentShader(VirtualDmdRenderStyle style)
		{
			// Single-sourced from DmdShaderSource (shared with the GL-ES pipeline). Desktop GLSL 1.20
			// header here; GL ES prepends its own #version 100 + precision.
			return DesktopHeader
				+ DmdShaderSource.BuildDmdConfig(
					style.DotSize, style.DotRounding, style.DotSharpness,
					style.Brightness, style.BackGlow, style.DotGlow, style.Gamma,
					style.HasBackGlow, style.HasDotGlow, style.HasBrightness,
					style.HasUnlitDot, style.HasGlass && _glassRgba != null, style.HasGamma)
				+ DmdShaderSource.DmdFragmentBody;
		}

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
		private delegate void GlUniform4f(int location, float value0, float value1, float value2, float value3);
		private delegate void GlUseProgram(uint program);
		private delegate void GlBegin(uint mode);
		private delegate void GlBindTexture(uint target, uint texture);
		private delegate void GlClear(uint mask);
		private delegate void GlClearColor(float red, float green, float blue, float alpha);
		private delegate void GlColor3f(float red, float green, float blue);
		private delegate void GlDisable(uint cap);
		private delegate void GlEnable(uint cap);
		private delegate void GlEnd();
		private delegate void GlGenTextures(int n, uint[] textures);
		private delegate void GlLoadIdentity();
		private delegate void GlMatrixMode(uint mode);
		private delegate void GlOrtho(double left, double right, double bottom, double top, double zNear, double zFar);
		private delegate void GlPixelStorei(uint pname, int param);
		private delegate void GlTexCoord2f(float s, float t);
		private delegate void GlTexImage2DIntPtr(uint target, int level, uint internalFormat, int width, int height, int border, uint format, uint type, IntPtr pixels);
		private delegate void GlTexImage2DBytes(uint target, int level, uint internalFormat, int width, int height, int border, uint format, uint type, byte[] pixels);
		private delegate void GlTexParameteri(uint target, uint pname, int param);
		private delegate void GlTexSubImage2D(uint target, int level, int xoffset, int yoffset, int width, int height, uint format, uint type, byte[] pixels);
		private delegate void GlVertex2f(float x, float y);
		private delegate void GlViewport(int x, int y, int width, int height);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("opengl32.dll")]
		private static extern IntPtr wglGetProcAddress(string lpszProc);
	}
}

