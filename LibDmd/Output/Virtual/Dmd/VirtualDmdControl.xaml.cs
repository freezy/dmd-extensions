using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;
using SharpGL;
using SharpGL.Shaders;
using SharpGL.VertexBuffers;
using SharpGL.WPF;

// This used to be standard WPF code with ShaderEffect, the problem is that WPF will always resize the provided ImageBrush
// (see https://docs.microsoft.com/en-us/archive/blogs/greg_schechter/introducing-multi-input-shader-effects) and therefore
// produce unacceptable artifacts.
//
// This lead to move toward SharpGL (OpenGL) for a clean, fast and artifact free implementation.
namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IRgb24Destination, IColoredGrayDestination, IBitmapDestination, IResizableDestination, IVirtualControl
	// these others are for debugging purpose. basically you can make the virtual dmd
	// behave like any other display by adding/removing interfaces
	// standard (aka production); IRgb24Destination, IBitmapDestination, IResizableDestination
	// pindmd1/2: IGray2Destination, IGray4Destination, IResizableDestination, IFixedSizeDestination
	// pin2dmd: IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IFixedSizeDestination
	// pindmd3: IGray2Destination, IGray4Destination, IColoredGray2Destination, IFixedSizeDestination
	{

		public VirtualDisplay Host { set; get; }

		public bool IsAvailable { get; } = true;

		public int DmdWidth { get; private set; } = 128;

		public int DmdHeight { get; private set; } = 32;

		public double AspectRatio { get; private set; } = 1.0;

		public bool IgnoreAspectRatio
		{
			get { return _ignoreAr; }
			set
			{
				_ignoreAr = value;
				OnSizeChanged(null, null);
			}
		}

		private bool _ignoreAr = true;
		private DmdStyle _style = new DmdStyle();
		private Color _dotColor;
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;
		private bool _fboInvalid = true; // Flag set to true when the FBOs (Framebuffer object) need to be rebuilt (for example at startup or when DMD size changes)
		private bool _lutInvalid = true; // Flag set to true when the LUT (look up table) of the palette has changed and needs to be updated on the GPU
		private bool _dmdShaderInvalid = true; // Flag set to true when the DMD shader needs to be rebuilt (for example at startup or when the DMD style change)
		private System.Drawing.Bitmap _glassToRender; // Set to the bitmap to be upload to GPU for the glass, nullified once uploaded
		private bool _hasFrame; // Flag set to true when a new frame is to be processed (following a call to RenderXXX)
		private FrameFormat _nextFrameType = FrameFormat.AlphaNumeric; // Format of the frame to be processed
		private BitmapSource _nextFrameBitmap; // Bitmap of the frame to be processed if RenderBitmap was called
		private byte[] _nextFrameData; // Raw data of the frame to be processed
		private FrameFormat _convertShaderType = FrameFormat.AlphaNumeric; // Format of the compiled convert shader; if frame format, the shader will be recompiled to adapt to the incoming frame
		private ShaderProgram _convertShader, _blurShader1, _blurShader2, _dmdShader;
		private int _csTexture, _csPalette; // Convert Shader (hence the _cs prefix) uniform locations
		private int _bs1Texture, _bs1Direction; // Blur Shader 1 (hence the _bs1 prefix) uniform locations
		private int _bs2Texture, _bs2Direction; // Blur Shader 2 (hence the _bs2 prefix) uniform locations
		private int _dsDmdTexture, _dsDmdDotGlow, _dsDmdBackGlow, _dsDmdSize, _dsUnlitDot; // Dmd Shader (hence the _ds prefix) uniform locations
		private int _dsGlassTexture, _dsGlassTexOffset, _dsGlassTexScale, _dsGlassColor;
		private readonly uint[] _textures = new uint[8]; // The 8 textures are: glass, palette LUT, input data, dmd, dot glow, intermediate blur, back blur, temp
		private readonly uint[] _fbos = new uint[5]; // The 5 FBO used to write to the 5 last textures (dmd, dot glow, intermediate blur, back blur, temp)
		private VertexBufferArray _quadVbo; // Cached VBO (vertex buffer object) used to render a full viewport quad
		private const uint PositionAttribute = 0; // Fixed index of position attribute in the quad VBO
		private const uint TexCoordAttribute = 1; // Fixed index of texture attribute in the quad VBO
		private readonly Dictionary<uint, string> _attributeLocations = new Dictionary<uint, string> { { PositionAttribute, "Position" }, { TexCoordAttribute, "TexCoord" }, };
		
		private const ushort FboErrorMax = 30;
		private ushort _fboErrorCount = 0;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdControl()
		{
			InitializeComponent();
			SizeChanged += OnSizeChanged;
			ClearColor();
			_fboErrorCount = 0;
		}
		public void SetStyle(DmdStyle style, string dataPath)
		{
			_style = style;
			_dmdShaderInvalid = true;
			_lutInvalid = true;
			var glassTexturePath = GetAbsolutePath(_style.GlassTexture, dataPath);
			try {
				_glassToRender = string.IsNullOrEmpty(glassTexturePath)
					? null
					: new System.Drawing.Bitmap(glassTexturePath);

			} catch (Exception e) {
				Logger.Warn(e, $"Could not load glass texture at \"{glassTexturePath}\".");
				_glassToRender = null;
			}

			var frameTexturePath = GetAbsolutePath(_style.FrameTexture, dataPath);
			try {
				if (!string.IsNullOrEmpty(frameTexturePath)) {
					var image = new BitmapImage(new Uri(frameTexturePath));
					DmdFraming.Source = image;
					DmdFraming.Visibility = Visibility.Visible;
				} else {
					DmdFraming.Visibility = Visibility.Hidden;
				}

			} catch (Exception e) {
				Logger.Warn(e, $"Could not load framing texture at \"{frameTexturePath}\".");
				DmdFraming.Source = null;
				DmdFraming.Visibility = Visibility.Hidden;
			}

			OnSizeChanged(null, null);
		}

		private string GetAbsolutePath(string path, string dataPath)
		{
			if (string.IsNullOrWhiteSpace(path)) {
				return null;
			}
			if (!Path.IsPathRooted(path) && dataPath != null) {
				return Path.Combine(dataPath, path);
			}
			return Path.GetFullPath(path);
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			_hasFrame = true;
			_nextFrameType = FrameFormat.Bitmap;
			_nextFrameBitmap = bmp;
			Dmd.RequestRender();
		}

		public void RenderGray2(byte[] frame)
		{
			_hasFrame = true;
			_nextFrameType = FrameFormat.Gray2;
			_nextFrameData = frame;
			Dmd.RequestRender();
		}

		public void RenderGray4(byte[] frame)
		{
			_hasFrame = true;
			_nextFrameType = FrameFormat.Gray4;
			_nextFrameData = frame;
			Dmd.RequestRender();
		}

		public void RenderColoredGray(ColoredFrame frame)
		{
			_hasFrame = true;
			_nextFrameType = FrameFormat.Rgb24;
			SetDimensions(frame.Width, frame.Height);
			_nextFrameData = frame.Data;
			Dmd.RequestRender();
		}


		public void RenderRgb24(byte[] frame)
		{
			_hasFrame = true;
			_nextFrameType = FrameFormat.Rgb24;
			_nextFrameData = frame;
			Dmd.RequestRender();
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			SetPalette(frame.Palette);
			RenderGray2(FrameUtil.Join(DmdWidth, DmdHeight, frame.Planes));
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			SetPalette(frame.Palette);
			RenderGray4(FrameUtil.Join(DmdWidth, DmdHeight, frame.Planes));
		}

		public void SetDimensions(int width, int height)
		{
			if (DmdWidth != width || DmdHeight != height)
			{
				Logger.Info("Resizing virtual DMD to {0}x{1}", width, height);
				DmdWidth = width;
				DmdHeight = height;
				_fboInvalid = true;
				OnSizeChanged(null, null);
			}
		}

		private void LogErrors(string message)
		{
			// Don't throw an exception unless it's occuring too often.
			if (++_fboErrorCount >= FboErrorMax)
			{
				throw new ArgumentException(message);
			}

			Logger.Error(message);
		}

		#region OpenGL

		public const string LIBRARY_OPENGL = "opengl32.dll"; // Opengl dll is named opengl32.dll for x86 as well as x64 archs for historical reasons
		[DllImport(LIBRARY_OPENGL, SetLastError = true)] private static extern void glTexSubImage2D(uint target, int level, int xoffset, int yoffset, int width, int height, uint format, uint type, byte[] pixels);
		[DllImport(LIBRARY_OPENGL, SetLastError = true)] private static extern void glTexSubImage2D(uint target, int level, int xoffset, int yoffset, int width, int height, uint format, uint type, IntPtr pixels);

		private void ogl_OpenGLInitialized(object sender, OpenGLRoutedEventArgs args)
		{
			var gl = args.OpenGL;
			gl.GenTextures(3, _textures);
			_fbos[0] = _fbos[1] = _fbos[2] = _fbos[3] = _fbos[4] = 0;
			_textures[3] = _textures[4] = _textures[5] = _textures[6] = _textures[7] = 0;
			try
			{
				_blurShader1 = new ShaderProgram();
				var frag = ReadResource(@"LibDmd.Output.Virtual.Dmd.Blur.frag") + "void main() { FragColor = vec4(blur_level_2(texture, uv, direction).rgb, 1.0); }";
				_blurShader1.Create(gl, ReadResource(@"LibDmd.Output.Virtual.Dmd.Blur.vert"), frag, _attributeLocations);
				_bs1Texture = _blurShader1.GetUniformLocation(gl, "texture");
				_bs1Direction = _blurShader1.GetUniformLocation(gl, "direction");
			}
			catch (ShaderCompilationException e)
			{
				Logger.Error(e, "Blur Shader 1 compilation failed");
				Logger.Error(e.CompilerOutput);
			}
			try
			{
				_blurShader2 = new ShaderProgram();
				var frag = ReadResource(@"LibDmd.Output.Virtual.Dmd.Blur.frag") + "void main() { FragColor = vec4(blur_level_12(texture, uv, direction).rgb, 1.0); }";
				_blurShader2.Create(gl, ReadResource(@"LibDmd.Output.Virtual.Dmd.Blur.vert"), frag, _attributeLocations);
				_bs2Texture = _blurShader2.GetUniformLocation(gl, "texture");
				_bs2Direction = _blurShader2.GetUniformLocation(gl, "direction");
			}
			catch (ShaderCompilationException e)
			{
				Logger.Error(e, "Blur Shader 2 compilation failed");
				Logger.Error(e.CompilerOutput);
			}
			_quadVbo = new VertexBufferArray();
			_quadVbo.Create(gl);
			_quadVbo.Bind(gl);
			var posVBO = new VertexBuffer();
			posVBO.Create(gl);
			posVBO.Bind(gl);
			posVBO.SetData(gl, PositionAttribute, new float[] { -1f, -1f, -1f, 1f, 1f, 1f, 1f, -1f }, false, 2);
			var texVBO = new VertexBuffer();
			texVBO.Create(gl);
			texVBO.Bind(gl);
			texVBO.SetData(gl, TexCoordAttribute, new float[] { 0f, 1f, 0f, 0f, 1f, 0f, 1f, 1f }, false, 2);
			_quadVbo.Unbind(gl);
		}

		private void ogl_OpenGLDraw(object sender, OpenGLRoutedEventArgs args)
		{
			var gl = args.OpenGL;
			gl.ClearColor(0f, 0f, 0f, 1f);
			gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
			var createTexture = false;

			if (_dmdShaderInvalid)
			{
				// Create a dedicated DMD shader based on the selected style settings
				createTexture = true;
				_dmdShaderInvalid = false;
				_dmdShader?.Delete(gl);
				try
				{
					_dmdShader = new ShaderProgram();
					var code = new StringBuilder();
					code.Append("#version 130\n");
					if (_style.HasBackGlow) code.Append("#define BACKGLOW\n");
					if (_style.HasDotGlow) code.Append("#define DOTGLOW\n");
					if (_style.HasBrightness) code.Append("#define BRIGHTNESS\n");
					if (_style.HasUnlitDot) code.Append("#define UNLIT\n");
					if (_style.HasGlass) code.Append("#define GLASS\n");
					if (_style.HasGamma) code.Append("#define GAMMA\n");
					if (_style.DotSize > 0.5) code.Append("#define DOT_OVERLAP\n");
					var nfi = System.Globalization.NumberFormatInfo.InvariantInfo;
					code.AppendFormat(nfi, "const float dotSize = {0:0.00000};\n", _style.DotSize);
					code.AppendFormat(nfi, "const float dotRounding = {0:0.00000};\n", _style.DotRounding);
					code.AppendFormat(nfi, "const float sharpMax = {0:0.00000};\n", 0.01 + _style.DotSize * (1.0 - _style.DotSharpness));
					code.AppendFormat(nfi, "const float sharpMin = {0:0.00000};\n", -0.01 -_style.DotSize * (1.0 - _style.DotSharpness));
					code.AppendFormat(nfi, "const float brightness = {0:0.00000};\n", _style.Brightness);
					code.AppendFormat(nfi, "const float backGlow = {0:0.00000};\n", _style.BackGlow);
					code.AppendFormat(nfi, "const float dotGlow = {0:0.00000};\n", _style.DotGlow);
					code.AppendFormat(nfi, "const float gamma = {0:0.00000};\n", _style.Gamma);
					code.Append(ReadResource(@"LibDmd.Output.Virtual.Dmd.Dmd.frag"));
					_dmdShader.Create(gl, ReadResource(@"LibDmd.Output.Virtual.Dmd.Dmd.vert"), code.ToString(), _attributeLocations);
					_dsDmdTexture = _dmdShader.GetUniformLocation(gl, "dmdTexture");
					_dsDmdDotGlow = _dmdShader.GetUniformLocation(gl, "dmdDotGlow");
					_dsDmdBackGlow = _dmdShader.GetUniformLocation(gl, "dmdBackGlow");
					_dsDmdSize = _dmdShader.GetUniformLocation(gl, "dmdSize");
					_dsUnlitDot = _dmdShader.GetUniformLocation(gl, "unlitDot");
					_dsGlassTexture = _dmdShader.GetUniformLocation(gl, "glassTexture");
					_dsGlassTexOffset = _dmdShader.GetUniformLocation(gl, "glassTexOffset");
					_dsGlassTexScale = _dmdShader.GetUniformLocation(gl, "glassTexScale");
					_dsGlassColor = _dmdShader.GetUniformLocation(gl, "glassColor");
				}
				catch (ShaderCompilationException e)
				{
					Logger.Error(e, "DMD Shader compilation failed:");
					Logger.Error(e.CompilerOutput);
				}
			}

			if (_fboInvalid)
			{
				_fboInvalid = false;
				createTexture = true;
				// Release previous textures and FBOs if any (0 are ignored by OpenGL driver)
				uint[] texs = new uint[5] { _textures[3], _textures[4], _textures[5], _textures[6], _textures[7] };
				gl.DeleteTextures(5, texs);
				gl.DeleteFramebuffersEXT(5, _fbos);
				Logger.Info("Creating FBOs for {0}x{1}", DmdWidth, DmdHeight);
				gl.GenTextures(5, texs);
				gl.GenFramebuffersEXT(5, _fbos);
				_textures[3] = texs[0];
				_textures[4] = texs[1];
				_textures[5] = texs[2];
				_textures[6] = texs[3];
				_textures[7] = texs[4];
				for (int i = 0; i < _fbos.Length; i++)
				{
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[i]);
					gl.BindTexture(OpenGL.GL_TEXTURE_2D, _textures[i + 3]);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_BORDER_COLOR, new float[] { 0f, 0f, 0f, 0f });
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_BORDER);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_BORDER);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
					gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, DmdWidth, DmdHeight, 0, OpenGL.GL_RGB, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
					gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, _textures[i + 3], 0);
					uint status = gl.CheckFramebufferStatusEXT(OpenGL.GL_FRAMEBUFFER_EXT);
					switch (status)
					{
						case OpenGL.GL_FRAMEBUFFER_COMPLETE_EXT:
							break;
						case OpenGL.GL_FRAMEBUFFER_UNSUPPORTED_EXT:
							Logger.Info("Failed to build FBO for virtual DMD [error: GL_FRAMEBUFFER_UNSUPPORTED_EXT]");
							break;
						default:
							Logger.Info("Failed to build FBO for virtual DMD [{0}]", status);
							break;
					}
				}
			}

			_quadVbo.Bind(gl);

			for (int i = 0; i < _textures.Length; i++)
			{
				gl.ActiveTexture(OpenGL.GL_TEXTURE0 + (uint)i);
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, _textures[i]);
			}

			if (_glassToRender != null)
			{
				// Upload glass bitmap to GPU, generate mipmaps, then release bitmap
				gl.ActiveTexture(OpenGL.GL_TEXTURE0);
				var data = _glassToRender.LockBits(new System.Drawing.Rectangle(0, 0, _glassToRender.Width, _glassToRender.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb).Scan0;
				gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, _glassToRender.Width, _glassToRender.Height, 0, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, data);
				gl.GenerateMipmapEXT(OpenGL.GL_TEXTURE_2D);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR_MIPMAP_LINEAR);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
				_glassToRender = null;
			}

			if (_hasFrame)
			{
				// Update palette (small 16x1 texture used as a LUT (lookup table) when processing the DMD data on the GPU)
				if (_lutInvalid)
				{
					_lutInvalid = false;
					byte[] data = new byte[3 * 16];
					if (_nextFrameType == FrameFormat.Gray2 && _gray2Palette != null)
					{
						for (int i = 0; i < 16; i++)
						{
							data[i * 3] = _gray2Palette[i / 4].R;
							data[i * 3 + 1] = _gray2Palette[i / 4].G;
							data[i * 3 + 2] = _gray2Palette[i / 4].B;
						}
					}
					else if (_nextFrameType == FrameFormat.Gray4 && _gray4Palette != null)
					{
						for (int i = 0; i < 16; i++)
						{
							data[i * 3] = _gray4Palette[i].R;
							data[i * 3 + 1] = _gray4Palette[i].G;
							data[i * 3 + 2] = _gray4Palette[i].B;
						}
					}
					else
					{
						var alpha = 1.0f - _style.Tint.ScA;
						var beta = _style.Tint.ScA;
						ColorUtil.RgbToHsl(_dotColor.R, _dotColor.G, _dotColor.B, out var dotHue, out var dotSat, out var dotLum);
						ColorUtil.RgbToHsl(_style.Tint.R, _style.Tint.G, _style.Tint.B, out var tintHue, out var tintSat, out var tintLum);
						for (int i = 0; i < 16; i++)
						{
							ColorUtil.HslToRgb(dotHue, dotSat, dotLum * i / 15.0, out var dotRed, out var dotGreen, out var dotBlue);
							ColorUtil.HslToRgb(tintHue, tintSat, tintLum * i / 15.0, out var tintRed, out var tintGreen, out var tintBlue);
							var red = (byte)(dotRed * alpha + tintRed * beta);
							var green = (byte)(dotGreen * alpha + tintGreen * beta);
							var blue = (byte)(dotBlue * alpha + tintBlue * beta);
							data[i * 3] = red;
							data[i * 3 + 1] = green;
							data[i * 3 + 2] = blue;
						}
					}
					gl.ActiveTexture(OpenGL.GL_TEXTURE1);
					gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, 16, 1, 0, OpenGL.GL_RGB, OpenGL.GL_UNSIGNED_BYTE, data);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
				}

				if (_convertShader == null || _convertShaderType != _nextFrameType)
				{
					_convertShader?.Delete(gl);
					_convertShaderType = _nextFrameType;
					createTexture = true;
					try
					{
						_convertShader = new ShaderProgram();
						var code = new StringBuilder();
						code.Append("#version 130\n");
						code.Append("#define ");
						code.Append(_convertShaderType.ToString().ToUpperInvariant());
						code.Append("\n");
						if (_style.HasGamma) code.Append("#define GAMMA\n");
						var nfi = System.Globalization.NumberFormatInfo.InvariantInfo;
						code.AppendFormat(nfi, "const float gamma = {0:0.00000};\n", _style.Gamma);
						code.AppendFormat(nfi, "const int dmdWidth = {0};\n", DmdWidth);
						code.Append(ReadResource(@"LibDmd.Output.Virtual.Dmd.Convert.frag"));
						_convertShader.Create(gl, ReadResource(@"LibDmd.Output.Virtual.Dmd.Convert.vert"), code.ToString(), _attributeLocations);
						_csTexture = _convertShader.GetUniformLocation(gl, "dmdData");
						_csPalette = _convertShader.GetUniformLocation(gl, "palette");
					}
					catch (ShaderCompilationException e)
					{
						Logger.Error(e, "Convert shader compilation failed");
						Logger.Error(e.CompilerOutput);
					}

				}

				// Update DMD texture with latest frame
				_hasFrame = false;
				gl.ActiveTexture(OpenGL.GL_TEXTURE2);
				switch (_nextFrameType)
				{
					case FrameFormat.Gray2:
						if (_nextFrameData.Length != DmdWidth * DmdHeight)
						{
							LogErrors("Invalid frame buffer size of [" + _nextFrameData.Length + "] bytes for a frame size of [" + DmdWidth + " x " + DmdHeight + "]");
							return;
						}
						if (createTexture)
							gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_LUMINANCE8, DmdWidth, DmdHeight, 0, OpenGL.GL_LUMINANCE, OpenGL.GL_UNSIGNED_BYTE, _nextFrameData);
						else
							glTexSubImage2D(OpenGL.GL_TEXTURE_2D, 0, 0, 0, DmdWidth, DmdHeight, OpenGL.GL_LUMINANCE, OpenGL.GL_UNSIGNED_BYTE, _nextFrameData);
						break;
					case FrameFormat.Gray4:
						if (_nextFrameData.Length != DmdWidth * DmdHeight)
						{
							LogErrors("Invalid frame buffer size of [" + _nextFrameData.Length + "] bytes for a frame size of [" + DmdWidth + " x " + DmdHeight + "]");
							return;
						}
						if (createTexture)
							gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_LUMINANCE8, DmdWidth, DmdHeight, 0, OpenGL.GL_LUMINANCE, OpenGL.GL_UNSIGNED_BYTE, _nextFrameData);
						else
							glTexSubImage2D(OpenGL.GL_TEXTURE_2D, 0, 0, 0, DmdWidth, DmdHeight, OpenGL.GL_LUMINANCE, OpenGL.GL_UNSIGNED_BYTE, _nextFrameData);
						break;
					case FrameFormat.Rgb24:
						if (_nextFrameData.Length % 3 != 0)
						{
							LogErrors("RGB24 buffer must be divisible by 3, but " + _nextFrameData.Length + " isn't.");
							return;
						}
						if (createTexture)
							gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, DmdWidth, DmdHeight, 0, OpenGL.GL_RGB, OpenGL.GL_UNSIGNED_BYTE, _nextFrameData);
						else
							glTexSubImage2D(OpenGL.GL_TEXTURE_2D, 0, 0, 0, DmdWidth, DmdHeight, OpenGL.GL_RGB, OpenGL.GL_UNSIGNED_BYTE, _nextFrameData);
						break;
					case FrameFormat.Bitmap:
						var _bitmapToRender = ImageUtil.ConvertToImage(_nextFrameBitmap) as System.Drawing.Bitmap;
						var data = _bitmapToRender.LockBits(new System.Drawing.Rectangle(0, 0, _bitmapToRender.Width, _bitmapToRender.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
						if (createTexture)
							gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, _bitmapToRender.Width, _bitmapToRender.Height, 0, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, data.Scan0);
						else
							glTexSubImage2D(OpenGL.GL_TEXTURE_2D, 0, 0, 0, _bitmapToRender.Width, _bitmapToRender.Height, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, data.Scan0);
						_bitmapToRender.UnlockBits(data);
						break;
				}
				if (createTexture)
				{
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_BORDER_COLOR, new float[] { 0f, 0f, 0f, 0f });
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_BORDER);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_BORDER);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
				}

				// Apply palette, tinting and gamma
				_convertShader.Bind(gl);
				gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[0]);
				gl.Viewport(0, 0, DmdWidth, DmdHeight);
				gl.Uniform1(_csPalette, 1); // Color palette
				gl.Uniform1(_csTexture, 2); // DMD texture
				gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
				_convertShader.Unbind(gl);

				// Compute blur levels
				if (_style.HasGlass || _style.HasDotGlow || _style.HasBackGlow)
				{
					_blurShader1.Bind(gl);
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[4]); // Horizontal pass (from last blur level, to temp FBO (Tex #7))
					gl.Viewport(0, 0, DmdWidth, DmdHeight);
					gl.Uniform1(_bs1Texture, 3); // DMD texture
					gl.Uniform2(_bs1Direction, 1.0f / DmdWidth, 0.0f);
					gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[1]); // Vertical pass (from temp to destination FBO)
					gl.Viewport(0, 0, DmdWidth, DmdHeight);
					gl.Uniform1(_bs1Texture, 7);
					gl.Uniform2(_bs1Direction, 0.0f, 1.0f / DmdHeight);
					gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
					_blurShader1.Unbind(gl);

					_blurShader2.Bind(gl);
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[4]); // Horizontal pass (from last blur level, to temp FBO (Tex #7))
					gl.Viewport(0, 0, DmdWidth, DmdHeight);
					gl.Uniform1(_bs2Texture, 4); // Previous Blur
					gl.Uniform2(_bs2Direction, 1.0f / DmdWidth, 0.0f);
					gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[2]); // Vertical pass (from temp to destination FBO)
					gl.Viewport(0, 0, DmdWidth, DmdHeight);
					gl.Uniform1(_bs2Texture, 7);
					gl.Uniform2(_bs2Direction, 0.0f, 1.0f / DmdHeight);
					gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);

					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[4]); // Horizontal pass (from last blur level, to temp FBO (Tex #7))
					gl.Viewport(0, 0, DmdWidth, DmdHeight);
					gl.Uniform1(_bs2Texture, 5); // Previous Blur
					gl.Uniform2(_bs2Direction, 1.0f / DmdWidth, 0.0f);
					gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[3]); // Vertical pass (from temp to destination FBO)
					gl.Viewport(0, 0, DmdWidth, DmdHeight);
					gl.Uniform1(_bs2Texture, 7);
					gl.Uniform2(_bs2Direction, 0.0f, 1.0f / DmdHeight);
					gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
					_blurShader2.Unbind(gl);
				}
			}

			// Render Dmd
			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
			gl.Viewport(0, 0, (int)Dmd.Width, (int)Dmd.Height);
			_dmdShader.Bind(gl);
			if (_dsGlassTexture != -1) gl.Uniform1(_dsGlassTexture, 0);
			if (_dsDmdTexture != -1) gl.Uniform1(_dsDmdTexture, 3);
			if (_dsDmdDotGlow != -1) gl.Uniform1(_dsDmdDotGlow, 4);
			if (_dsDmdBackGlow != -1) gl.Uniform1(_dsDmdBackGlow, 6);
			if (_dsDmdSize != -1) gl.Uniform2(_dsDmdSize, (float)DmdWidth, DmdHeight);
			if (_dsUnlitDot != -1) gl.Uniform3(_dsUnlitDot, (float)(_style.UnlitDot.ScR / _style.Brightness), (float)(_style.UnlitDot.ScG / _style.Brightness), (float)(_style.UnlitDot.ScB / _style.Brightness));
			if (_dsGlassTexOffset != -1) gl.Uniform2(_dsGlassTexOffset, (float)(_style.GlassPadding.Left / DmdWidth), (float)(_style.GlassPadding.Top / DmdHeight));
			if (_dsGlassTexScale != -1) gl.Uniform2(_dsGlassTexScale, (float)(1f + (_style.GlassPadding.Left + _style.GlassPadding.Right) / DmdWidth), (float)(1f + (_style.GlassPadding.Top + _style.GlassPadding.Bottom) / DmdHeight));
			if (_dsGlassColor != -1) gl.Uniform4(_dsGlassColor, _style.GlassColor.ScR, _style.GlassColor.ScG, _style.GlassColor.ScB, (float)_style.GlassLighting);
			gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
			_dmdShader.Unbind(gl);

			_quadVbo.Unbind(gl);
		}

		#endregion

		private void OnSizeChanged(object sender, RoutedEventArgs e)
		{
			// Resize events should reset this error count.
			_fboErrorCount = 0;

			if (!Dispatcher.HasShutdownFinished || !Dispatcher.HasShutdownStarted) {
				try {
					Dispatcher.Invoke(() => {
						var glassWidth = DmdWidth + _style.GlassPadding.Left + _style.GlassPadding.Right;
						var glassHeight = DmdHeight + _style.GlassPadding.Top + _style.GlassPadding.Bottom;

						var frameWidth = glassWidth + _style.FramePadding.Left + _style.FramePadding.Right;
						var frameHeight = glassHeight + _style.FramePadding.Top + _style.FramePadding.Bottom;
						AspectRatio = frameWidth / (double)frameHeight;

						var alphaW = ActualWidth / frameWidth;
						var alphaH = ActualHeight / frameHeight;
						if (!IgnoreAspectRatio)
						{
							var alpha = Math.Min(alphaW, alphaH);
							alphaW = alpha;
							alphaH = alpha;
						}

						var hpad = 0.5 * (ActualWidth - frameWidth * alphaW);
						var vpad = 0.5 * (ActualHeight - frameHeight * alphaH);

						DmdFraming.Width = frameWidth * alphaW;
						DmdFraming.Height = frameHeight * alphaH;
						DmdFraming.Margin = new Thickness(hpad, vpad, hpad, vpad);

						Dmd.Width = glassWidth * alphaW;
						Dmd.Height = glassHeight * alphaH;
						Dmd.Margin = new Thickness(hpad + _style.FramePadding.Left * alphaW, vpad + _style.FramePadding.Top * alphaH, hpad + _style.FramePadding.Right * alphaW, vpad + _style.FramePadding.Bottom * alphaH);

						Host?.SetDimensions((int)frameWidth, (int)frameHeight);
					});
				
				} catch (TaskCanceledException ex) {

					Logger.Error(ex, "Main thread seems already destroyed, aborting.");
				}
			}
		}

		public void SetColor(Color color)
		{
			_lutInvalid = true;
			_dotColor = color;
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			_lutInvalid = true;
			_gray2Palette = ColorUtil.GetPalette(colors, 4);
			_gray4Palette = ColorUtil.GetPalette(colors, 16);
		}

		public void ClearPalette()
		{
			_lutInvalid = true;
			_gray2Palette = null;
			_gray4Palette = null;
		}

		public void ClearColor()
		{
			SetColor(RenderGraph.DefaultColor);
		}

		public void Init()
		{
			// ReSharper disable once SuspiciousTypeConversion.Global
			if (this is IFixedSizeDestination)
			{
				SetDimensions(DmdWidth, DmdHeight);
			}
		}

		public void ClearDisplay()
		{
			RenderGray4(new byte[DmdWidth * DmdHeight]);
		}

		private static string ReadResource(string name)
		{
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
			using (StreamReader reader = new StreamReader(stream))
			{
				return reader.ReadToEnd();
			}
		}

		public void Dispose()
		{
			// FIXME we should dispose the OpenGL native objects allocated in ogl_Initalized but this need to have the OpenGL context which is not garanteed here
		}
	}
}
