using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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

		public double DotSize { get; set; }

		public double DotRounding { get; set; }

		public double UnlitDot { get; set; }

		public double DotGlow { get; set; }

		public double BackGlow { get; set; }

		public double Brightness { get; set; }

		public Color GlassColor { get; set; }

		public string Glass
		{
			get { return _glass; }
			set
			{
				_glass = value;
				try
				{
					glassToRender = new System.Drawing.Bitmap(_glass);
				}
				catch
				{
					glassToRender = null;
				}
			}
		}

		public Thickness GlassPad
		{
			get { return _glassPad; }
			set
			{
				_glassPad = value;
				OnSizeChanged(null, null);
			}
		}

		public string Frame
		{
			get { return _frame; }
			set
			{
				_frame = value;
				try
				{
					var image = new BitmapImage(new Uri(_frame));
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
			}
		}

		public Thickness FramePad
		{
			get { return _framePad; }
			set
			{
				_framePad = value;
				OnSizeChanged(null, null);
			}
		}

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
		private string _glass = null;
		private Thickness _glassPad = new Thickness(0);
		private string _frame = null;
		private Thickness _framePad = new Thickness(0);
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;
		private bool fboInvalid = true;
		private VertexBufferArray _quadVBO;
		private ShaderProgram _dmdShader, _blurShader;
		private readonly uint[] _textures = new uint[6];
		private readonly uint[] _fbos = new uint[4];

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdControl()
		{
			InitializeComponent();
			SizeChanged += OnSizeChanged;
			ClearColor();
		}

		private System.Drawing.Bitmap bitmapToRender = null;
		private System.Drawing.Bitmap glassToRender = null;

		private System.Drawing.Bitmap GammaCorrection(System.Drawing.Bitmap img, double gamma, double c = 1d)
		{
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
				bitmapToRender = GammaCorrection(ImageUtil.ConvertToImage(bmp) as System.Drawing.Bitmap, 2.2);
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
				fboInvalid = true;
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
			// FIXME these native objects should be released when the DMD is closed (but this need to have the OpenGL context active while closing the window)
			var gl = args.OpenGL;

			gl.GenTextures(2, _textures);
			_fbos[0] = _fbos[1] = _fbos[2] = _fbos[3] = 0;
			_textures[2] = _textures[3] = _textures[4] = _textures[5] = 0;

			const uint positionAttribute = 0;
			const uint texCoordAttribute = 1;
			var attributeLocations = new Dictionary<uint, string> { { positionAttribute, "Position" }, { texCoordAttribute, "TexCoord" }, };
			try
			{
				_dmdShader = new ShaderProgram();
				_dmdShader.Create(gl, ReadResource(@"LibDmd.Output.Virtual.Dmd.Dmd.vert"), ReadResource(@"LibDmd.Output.Virtual.Dmd.Dmd.frag"), attributeLocations);
			}
			catch (Exception e)
			{
				Logger.Error(e, "DMD Shader compilation failed");
			}
			try
			{
				_blurShader = new ShaderProgram();
				_blurShader.Create(gl, ReadResource(@"LibDmd.Output.Virtual.Dmd.Blur.vert"), ReadResource(@"LibDmd.Output.Virtual.Dmd.Blur.frag"), attributeLocations);
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
			posVBO.SetData(gl, positionAttribute, new float[] { -1f, -1f, -1f, 1f, 1f, 1f, 1f, -1f }, false, 2);
			var texVBO = new VertexBuffer();
			texVBO.Create(gl);
			texVBO.Bind(gl);
			texVBO.SetData(gl, texCoordAttribute, new float[] { 0f, 1f, 0f, 0f, 1f, 0f, 1f, 1f }, false, 2);
			_quadVBO.Unbind(gl);
		}

		private void ogl_OpenGLDraw(object sender, OpenGLRoutedEventArgs args)
		{
			var gl = args.OpenGL;
			gl.ClearColor(0f, 0f, 0f, 1f);
			gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
			gl.Color(1f, 1f, 1f);

			if (fboInvalid)
			{
				// Release previous textures and FBOs if any (0 are ignored by OpenGL driver)
				uint[] texs = new uint[4] { _textures[2], _textures[3], _textures[4], _textures[5]};
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
				fboInvalid = false;
			}

			_quadVBO.Bind(gl);

			// Textures are: glass, dmd, blur 1, blur 2, blur 3, temp
			for (int i = 0; i < 6; i++)
			{
				gl.ActiveTexture(OpenGL.GL_TEXTURE0 + (uint)i);
				gl.BindTexture(OpenGL.GL_TEXTURE_2D, _textures[i]);
			}

			if (glassToRender != null)
			{
				Logger.Info("Glass texture updated");
				gl.ActiveTexture(OpenGL.GL_TEXTURE0);
				var data = glassToRender.LockBits(new System.Drawing.Rectangle(0, 0, glassToRender.Width, glassToRender.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb).Scan0;
				gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, glassToRender.Width, glassToRender.Height, 0, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, data);
				gl.GenerateMipmapEXT(OpenGL.GL_TEXTURE_2D);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_CLAMP, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_BORDER);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_BORDER);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR_MIPMAP_LINEAR);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
				glassToRender = null;
			}

			if (bitmapToRender != null)
			{
				gl.ActiveTexture(OpenGL.GL_TEXTURE1);
				var data = bitmapToRender.LockBits(new System.Drawing.Rectangle(0, 0, bitmapToRender.Width, bitmapToRender.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb).Scan0;
				gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGB, bitmapToRender.Width, bitmapToRender.Height, 0, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, data);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_CLAMP, OpenGL.GL_CLAMP_TO_EDGE);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_BORDER);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_BORDER);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
				gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
				bitmapToRender = null;
				_blurShader.Bind(gl);
				for (int i = 0; i < 3; i++)
				{
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[3]); // Horizontal pass (from last blur level, to temp FBO (Tex #5))
					gl.Viewport(0, 0, DmdWidth, DmdHeight);
					gl.Uniform1(_blurShader.GetUniformLocation(gl, "texture"), i + 1);
					gl.Uniform2(_blurShader.GetUniformLocation(gl, "resolution"), (float)DmdWidth, (float)DmdHeight);
					gl.Uniform2(_blurShader.GetUniformLocation(gl, "direction"), 1.0f, 0.0f);
					gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
					gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, _fbos[i]); // Vertical pass (from temp to destination FBO)
					gl.Viewport(0, 0, DmdWidth, DmdHeight);
					gl.Uniform1(_blurShader.GetUniformLocation(gl, "texture"), 5);
					gl.Uniform2(_blurShader.GetUniformLocation(gl, "resolution"), (float)DmdWidth, (float)DmdHeight);
					gl.Uniform2(_blurShader.GetUniformLocation(gl, "direction"), 0.0f, 1.0f);
					gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
				}
				_blurShader.Unbind(gl);
			}

			gl.BindFramebufferEXT(OpenGL.GL_FRAMEBUFFER_EXT, 0);
			gl.Viewport(0, 0, (int)Dmd.Width, (int)Dmd.Height);
			_dmdShader.Bind(gl);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "glassTexture"), 0);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "dmdTexture"), 1);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "dmdTextureBlur1"), 2);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "dmdTextureBlur2"), 3);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "dmdTextureBlur3"), 4);
			gl.Uniform2(_dmdShader.GetUniformLocation(gl, "dmdSize"), (float)DmdWidth, (float)DmdHeight);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "unlitDot"), (float)UnlitDot);
			gl.Uniform2(_dmdShader.GetUniformLocation(gl, "glassTexOffset"), (float)(_glassPad.Left / DmdWidth), (float)(_glassPad.Top / DmdHeight));
			gl.Uniform2(_dmdShader.GetUniformLocation(gl, "glassTexScale"), (float)(1f + (_glassPad.Left + _glassPad.Right) / DmdWidth), (float)(1f + (_glassPad.Top + _glassPad.Bottom) / DmdHeight));
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "backGlow"), (float)BackGlow);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "brightness"), (float)Brightness);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "dotSize"), (float)DotSize);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "dotRounding"), (float)DotRounding);
			gl.Uniform1(_dmdShader.GetUniformLocation(gl, "dotGlow"), (float)DotGlow);
			gl.Uniform4(_dmdShader.GetUniformLocation(gl, "glassColor"), GlassColor.ScR, GlassColor.ScG, GlassColor.ScB, GlassColor.ScA);
			gl.DrawArrays(OpenGL.GL_TRIANGLE_FAN, 0, 4);
			_dmdShader.Unbind(gl);

			_quadVBO.Unbind(gl);
		}

		private void OnSizeChanged(object sender, RoutedEventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				var glassWidth = DmdWidth + _glassPad.Left + _glassPad.Right;
				var glassHeight = DmdHeight + _glassPad.Top + _glassPad.Bottom;

				var frameWidth = glassWidth + _framePad.Left + _framePad.Right;
				var frameHeight = glassHeight + _framePad.Top + _framePad.Bottom;

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
				Dmd.Margin = new Thickness(hpad + _framePad.Left * alphaW, vpad + _framePad.Top * alphaH, hpad + _framePad.Right * alphaW, vpad + _framePad.Bottom * alphaH);

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
			// nothing to dispose
		}
	}
}
