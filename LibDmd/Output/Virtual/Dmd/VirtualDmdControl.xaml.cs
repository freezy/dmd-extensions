using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
	public partial class VirtualDmdControl : IRgb24Destination, IBitmapDestination, IResizableDestination, IVirtualControl
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

		private double _hue;
		private double _sat;
		private double _lum;
		private bool _ignoreAr = true;
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;
		private bool _fboInvalid = true;
		private bool _dmdShaderInvalid = true;
		private VertexBufferArray _quadVBO;
		private ShaderProgram _dmdShader, _blurShader;
		private int _bsTexture, _bsResolution, _bsDirection;
		private int _dsDmdTexture, _dsDmdTextureBlur1, _dsDmdTextureBlur2, _dsDmdTextureBlur3, _dsDmdSize;
		private int _dsUnlitDot, _dsBackGlow, _dsBrightness, _dsDotSize, _dsDotRounding, _dsDotGlow, _dsGamma;
		private int _dsGlassTexture, _dsGlassTexOffset, _dsGlassTexScale, _dsGlassColor;
		private readonly uint[] _textures = new uint[6];
		private readonly uint[] _fbos = new uint[4];
		private System.Drawing.Bitmap _bitmapToRender = null;
		private System.Drawing.Bitmap _glassToRender = null;
		private DmdStyle _style = new DmdStyle();
		private const uint PositionAttribute = 0;
		private const uint TexCoordAttribute = 1;
		private readonly Dictionary<uint, string> _attributeLocations = new Dictionary<uint, string> { { PositionAttribute, "Position" }, { TexCoordAttribute, "TexCoord" }, };

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdControl()
		{
			InitializeComponent();
			SizeChanged += OnSizeChanged;
			ClearColor();
		}

		public void SetStyle(DmdStyle style)
		{
			_style = style;
			_dmdShaderInvalid = true;
			try
			{
				_glassToRender = new System.Drawing.Bitmap(_style.GlassTexture);
			}
			catch
			{
				_glassToRender = null;
			}
			try
			{
				var image = new BitmapImage(new Uri(_style.FrameTexture));
				DmdFrame.Source = image;
				if (image != null)
					DmdFrame.Visibility = Visibility.Visible;
				else
					DmdFrame.Visibility = Visibility.Hidden;
			}
			catch
			{
				DmdFrame.Source = null;
				DmdFrame.Visibility = Visibility.Hidden;
			}
			OnSizeChanged(null, null);
		}

		private System.Drawing.Bitmap GammaCorrection(System.Drawing.Bitmap img, double gamma, double c = 1d)
		{
			if (!_style.HasGamma)
				return img;
			int width = img.Width;
			int height = img.Height;
			BitmapData srcData = img.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			int bytes = srcData.Stride * srcData.Height;
			byte[] buffer = new byte[bytes];
			byte[] result = new byte[bytes];
			Marshal.Copy(srcData.Scan0, buffer, 0, bytes);
			img.UnlockBits(srcData);
			int current = 0;
			int cChannels = 3;
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					current = y * srcData.Stride + x * 4;
					for (int i = 0; i < cChannels; i++)
					{
						double range = (double)buffer[current + i] / 255;
						double correction = c * Math.Pow(range, gamma);
						result[current + i] = (byte)(correction * 255);
					}
					result[current + 3] = 255;
				}
			}
			System.Drawing.Bitmap resImg = new System.Drawing.Bitmap(width, height);
			BitmapData resData = resImg.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			Marshal.Copy(result, 0, resData.Scan0, bytes);
			resImg.UnlockBits(resData);
			return resImg;
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			Dispatcher.Invoke(() =>
			{
				_bitmapToRender = GammaCorrection(ImageUtil.ConvertToImage(bmp) as System.Drawing.Bitmap, _style.Gamma);
			});
		}

		public void RenderGray2(byte[] frame)
		{
			if (_gray2Palette != null)
			{
				RenderRgb24(ColorUtil.ColorizeFrame(DmdWidth, DmdHeight, frame, _gray2Palette));
			}
			else
			{
				RenderBitmap(ImageUtil.ConvertFromGray2(DmdWidth, DmdHeight, frame, _hue, _sat, _lum));
			}
		}

		public void RenderGray4(byte[] frame)
		{
			if (_gray4Palette != null)
			{
				RenderRgb24(ColorUtil.ColorizeFrame(DmdWidth, DmdHeight, frame, _gray4Palette));
			}
			else
			{
				RenderBitmap(ImageUtil.ConvertFromGray4(DmdWidth, DmdHeight, frame, _hue, _sat, _lum));
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			if (frame.Length % 3 != 0)
			{
				throw new ArgumentException("RGB24 buffer must be divisible by 3, but " + frame.Length + " isn't.");
			}
			RenderBitmap(ImageUtil.ConvertFromRgb24(DmdWidth, DmdHeight, frame));
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

		private string ReadResource(string name)
		{
			using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
			using (StreamReader reader = new StreamReader(stream))
			{
				return reader.ReadToEnd();
			}
		}

		private void ogl_OpenGLInitialized(object sender, OpenGLRoutedEventArgs args)
		{
			var gl = args.OpenGL;
			gl.GenTextures(2, _textures);
			_fbos[0] = _fbos[1] = _fbos[2] = _fbos[3] = 0;
			_textures[2] = _textures[3] = _textures[4] = _textures[5] = 0;
			try
			{
				_blurShader = new ShaderProgram();
				_blurShader.Create(gl, ReadResource(@"LibDmd.Output.Virtual.Dmd.Blur.vert"), ReadResource(@"LibDmd.Output.Virtual.Dmd.Blur.frag"), _attributeLocations);
				_bsTexture = _blurShader.GetUniformLocation(gl, "texture");
				_bsResolution = _blurShader.GetUniformLocation(gl, "resolution");
				_bsDirection = _blurShader.GetUniformLocation(gl, "direction");
			}
			catch (Exception e)
			{
				Logger.Error(e, "Blur Shader compilation failed");
			}
			_quadVBO = new VertexBufferArray();
			_quadVBO.Create(gl);
			_quadVBO.Bind(gl);
			var posVBO = new VertexBuffer();
			posVBO.Create(gl);
			posVBO.Bind(gl);
			posVBO.SetData(gl, PositionAttribute, new float[] { -1f, -1f, -1f, 1f, 1f, 1f, 1f, -1f }, false, 2);
			var texVBO = new VertexBuffer();
			texVBO.Create(gl);
			texVBO.Bind(gl);
			texVBO.SetData(gl, TexCoordAttribute, new float[] { 0f, 1f, 0f, 0f, 1f, 0f, 1f, 1f }, false, 2);
			_quadVBO.Unbind(gl);
		}

		private void ogl_OpenGLDraw(object sender, OpenGLRoutedEventArgs args)
		{
			var gl = args.OpenGL;
			gl.ClearColor(0f, 0f, 0f, 1f);
			gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
			gl.Color(1f, 1f, 1f);

			if (_dmdShaderInvalid)
			{
				_dmdShaderInvalid = false;
				if (_dmdShader != null)
					_dmdShader.Delete(gl);
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
					code.Append(ReadResource(@"LibDmd.Output.Virtual.Dmd.Dmd.frag"));
					_dmdShader.Create(gl, ReadResource(@"LibDmd.Output.Virtual.Dmd.Dmd.vert"), code.ToString(), _attributeLocations);
					_dsDmdTexture = _dmdShader.GetUniformLocation(gl, "dmdTexture");
					_dsDmdTextureBlur1 = _dmdShader.GetUniformLocation(gl, "dmdTextureBlur1");
					_dsDmdTextureBlur2 = _dmdShader.GetUniformLocation(gl, "dmdTextureBlur2");
					_dsDmdTextureBlur3 = _dmdShader.GetUniformLocation(gl, "dmdTextureBlur3");
					_dsDmdSize = _dmdShader.GetUniformLocation(gl, "dmdSize");
					_dsUnlitDot = _dmdShader.GetUniformLocation(gl, "unlitDot");
					_dsBackGlow = _dmdShader.GetUniformLocation(gl, "backGlow");
					_dsBrightness = _dmdShader.GetUniformLocation(gl, "brightness");
					_dsDotSize = _dmdShader.GetUniformLocation(gl, "dotSize");
					_dsDotRounding = _dmdShader.GetUniformLocation(gl, "dotRounding");
					_dsDotGlow = _dmdShader.GetUniformLocation(gl, "dotGlow");
					_dsGamma = _dmdShader.GetUniformLocation(gl, "gamma");
					_dsGlassTexture = _dmdShader.GetUniformLocation(gl, "glassTexture");
					_dsGlassTexOffset = _dmdShader.GetUniformLocation(gl, "glassTexOffset");
					_dsGlassTexScale = _dmdShader.GetUniformLocation(gl, "glassTexScale");
					_dsGlassColor = _dmdShader.GetUniformLocation(gl, "glassColor");
				}
				catch (Exception e)
				{
					Logger.Error(e, "DMD Shader compilation failed");
				}
			}

			if (_fboInvalid)
			{
				_fboInvalid = false;
				// Release previous textures and FBOs if any (0 are ignored by OpenGL driver)
				uint[] texs = new uint[4] { _textures[2], _textures[3], _textures[4], _textures[5] };
				gl.DeleteTextures(4, texs);
				gl.DeleteFramebuffersEXT(4, _fbos);
				Logger.Info("Creating FBOs for {0}x{1}", DmdWidth, DmdHeight);
				gl.GenTextures(4, texs);
				gl.GenFramebuffersEXT(4, _fbos);
				_textures[2] = texs[0];
				_textures[3] = texs[1];
				_textures[4] = texs[2];
				_textures[5] = texs[3];
				for (int i = 0; i < _fbos.Length; i++)
				{
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[i]);
					gl.BindTexture(OpenGL.GL_TEXTURE_2D, _textures[i + 2]);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_CLAMP, OpenGL.GL_CLAMP_TO_EDGE);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_BORDER);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_BORDER);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
					gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
					gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, DmdWidth, DmdHeight, 0, OpenGL.GL_RGB, OpenGL.GL_UNSIGNED_BYTE, IntPtr.Zero);
					gl.FramebufferTexture2DEXT(OpenGL.GL_FRAMEBUFFER_EXT, OpenGL.GL_COLOR_ATTACHMENT0_EXT, OpenGL.GL_TEXTURE_2D, _textures[i + 2], 0);
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

			_quadVBO.Bind(gl);

			// Textures are: glass, dmd, blur 1, blur 2, blur 3, temp
			for (int i = 0; i < 6; i++)
			{
				gl.ActiveTexture(OpenGL.GL_TEXTURE0 + (uint)i);
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, _textures[i]);
			}

			if (_glassToRender != null)
			{
				Logger.Info("Glass texture updated");
				gl.ActiveTexture(OpenGL.GL_TEXTURE0);
				var data = _glassToRender.LockBits(new System.Drawing.Rectangle(0, 0, _glassToRender.Width, _glassToRender.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb).Scan0;
				gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, _glassToRender.Width, _glassToRender.Height, 0, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, data);
				gl.GenerateMipmapEXT(OpenGL.GL_TEXTURE_2D);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_CLAMP, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_BORDER);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_BORDER);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR_MIPMAP_LINEAR);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
				_glassToRender = null;
			}

			if (_bitmapToRender != null)
			{
				gl.ActiveTexture(OpenGL.GL_TEXTURE1);
				var data = _bitmapToRender.LockBits(new System.Drawing.Rectangle(0, 0, _bitmapToRender.Width, _bitmapToRender.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb).Scan0;
				gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, _bitmapToRender.Width, _bitmapToRender.Height, 0, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, data);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_CLAMP, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_BORDER);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_BORDER);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
				_bitmapToRender = null;
				if (_style.HasGlass || _style.HasDotGlow || _style.HasBackGlow)
				{
					_blurShader.Bind(gl);
					for (int i = 0; i < 3; i++)
					{
						gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[3]); // Horizontal pass (from last blur level, to temp FBO (Tex #5))
						gl.Viewport(0, 0, DmdWidth, DmdHeight);
						gl.Uniform1(_bsTexture, i + 1);
						gl.Uniform2(_bsResolution, (float)DmdWidth, DmdHeight);
						gl.Uniform2(_bsDirection, 1.0f, 0.0f);
						gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
						gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[i]); // Vertical pass (from temp to destination FBO)
						gl.Viewport(0, 0, DmdWidth, DmdHeight);
						gl.Uniform1(_bsTexture, 5);
						gl.Uniform2(_bsResolution, (float)DmdWidth, DmdHeight);
						gl.Uniform2(_bsDirection, 0.0f, 1.0f);
						gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
					}
					_blurShader.Unbind(gl);
				}
			}

			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
			gl.Viewport(0, 0, (int)Dmd.Width, (int)Dmd.Height);
			_dmdShader.Bind(gl);
			if (_dsDmdTexture != -1) gl.Uniform1(_dsDmdTexture, 1);
			if (_dsDmdTextureBlur1 != -1) gl.Uniform1(_dsDmdTextureBlur1, 2);
			if (_dsDmdTextureBlur2 != -1) gl.Uniform1(_dsDmdTextureBlur2, 3);
			if (_dsDmdTextureBlur3 != -1) gl.Uniform1(_dsDmdTextureBlur3, 4);
			if (_dsDmdSize != -1) gl.Uniform2(_dsDmdSize, (float)DmdWidth, DmdHeight);
			if (_dsUnlitDot != -1) gl.Uniform3(_dsUnlitDot, _style.UnlitDot.ScR, _style.UnlitDot.ScG, _style.UnlitDot.ScB);
			if (_dsBackGlow != -1) gl.Uniform1(_dsBackGlow, (float)_style.BackGlow);
			if (_dsBrightness != -1) gl.Uniform1(_dsBrightness, (float)_style.Brightness);
			if (_dsDotSize != -1) gl.Uniform1(_dsDotSize, (float)_style.DotSize);
			if (_dsDotRounding != -1) gl.Uniform1(_dsDotRounding, (float)_style.DotRounding);
			if (_dsDotGlow != -1) gl.Uniform1(_dsDotGlow, (float)_style.DotGlow);
			if (_dsGamma != -1) gl.Uniform1(_dsGamma, (float)_style.Gamma);
			if (_dsGlassTexture != -1) gl.Uniform1(_dsGlassTexture, 0);
			if (_dsGlassTexOffset != -1) gl.Uniform2(_dsGlassTexOffset, (float)(_style.GlassPadding.Left / DmdWidth), (float)(_style.GlassPadding.Top / DmdHeight));
			if (_dsGlassTexScale != -1) gl.Uniform2(_dsGlassTexScale, (float)(1f + (_style.GlassPadding.Left + _style.GlassPadding.Right) / DmdWidth), (float)(1f + (_style.GlassPadding.Top + _style.GlassPadding.Bottom) / DmdHeight));
			if (_dsGlassColor != -1) gl.Uniform4(_dsGlassColor, _style.GlassColor.ScR, _style.GlassColor.ScG, _style.GlassColor.ScB, (float)_style.GlassLighting);
			gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
			_dmdShader.Unbind(gl);

			_quadVBO.Unbind(gl);
		}

		private void OnSizeChanged(object sender, RoutedEventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
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

				DmdFrame.Width = frameWidth * alphaW;
				DmdFrame.Height = frameHeight * alphaH;
				DmdFrame.Margin = new Thickness(hpad, vpad, hpad, vpad);

				Dmd.Width = glassWidth * alphaW;
				Dmd.Height = glassHeight * alphaH;
				Dmd.Margin = new Thickness(hpad + _style.FramePadding.Left * alphaW, vpad + _style.FramePadding.Top * alphaH, hpad + _style.FramePadding.Right * alphaW, vpad + _style.FramePadding.Bottom * alphaH);

				if (Host != null) Host.SetDimensions((int)frameWidth, (int)frameHeight);
			});
		}

		public void SetColor(Color color)
		{
			ColorUtil.RgbToHsl(color.R, color.G, color.B, out _hue, out _sat, out _lum);
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			_gray2Palette = ColorUtil.GetPalette(colors, 4);
			_gray4Palette = ColorUtil.GetPalette(colors, 16);
		}

		public void ClearPalette()
		{
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

		public void Dispose()
		{
			// FIXME we should dispose the OpenGL native objects allocated in ogl_Initalized but this need to have the OpenGL context which is not garanteed here
		}
	}
}
