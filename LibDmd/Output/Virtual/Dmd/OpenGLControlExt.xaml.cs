using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SharpGL;
using SharpGL.RenderContextProviders;
using SharpGL.Version;
using SharpGL.WPF;

namespace LibDmd.Output.Virtual.Dmd
{
	/// <summary>
	/// Interaction logic for OpenGLControlExt.xaml
	/// 
	/// This is a slightly modified version of the one provided in SharpGL for better performance. The original SharpGL
	/// in the method GetFormatedBitmapSource calls BitmapConversion method that perform a GC for each frame. This 
	/// implementation uses proposed by ftlPhysicsGuy in this issue: https://github.com/dwmkerr/sharpgl/issues/121
	/// to avoid the GC.
	/// </summary>
	public partial class OpenGLControlExt : UserControl
	{
		// Fields to support the WritableBitmap method of rendering the image for display
		private byte[] m_imageBuffer;
		private WriteableBitmap m_writeableBitmap;
		private Int32Rect m_imageRect;
		private int m_imageStride;
		private double m_dpiX = 96;
		private double m_dpiY = 96;
		private PixelFormat m_format = PixelFormats.Bgra32;
		private int m_bytesPerPixel = 32 >> 3;

		/// <summary>
		/// Initializes a new instance of the <see cref="OpenGLControlExt"/> class.
		/// </summary>
		public OpenGLControlExt()
		{
			InitializeComponent();

			timer = new DispatcherTimer();

			Unloaded += OpenGLControlExt_Unloaded;
			Loaded += OpenGLControlExt_Loaded;
		}

		/// <summary>
		/// Handles the Loaded event of the OpenGLControlExt control.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="routedEventArgs">The <see cref="System.Windows.RoutedEventArgs"/> Instance containing the event data.</param>
		private void OpenGLControlExt_Loaded(object sender, RoutedEventArgs routedEventArgs)
		{
			SizeChanged += OpenGLControlExt_SizeChanged;

			UpdateOpenGLControl((int)RenderSize.Width, (int)RenderSize.Height);

			//  DispatcherTimer setup
			timer.Tick += timer_Tick;
			if (RenderTrigger == RenderTrigger.TimerBased)
			{
				timer.Start();
			}
		}

		/// <summary>
		/// Handles the Unloaded event of the OpenGLControl control.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="routedEventArgs">The <see cref="System.Windows.RoutedEventArgs"/> Instance containing the event data.</param>
		private void OpenGLControlExt_Unloaded(object sender, RoutedEventArgs routedEventArgs)
		{
			SizeChanged -= OpenGLControlExt_SizeChanged;

			timer.Stop();
			timer.Tick -= timer_Tick;
		}

		/// <summary>
		/// Handles the SizeChanged event of the OpenGLControl control.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="System.Windows.SizeChangedEventArgs"/> Instance containing the event data.</param>
		void OpenGLControlExt_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateOpenGLControl((int)e.NewSize.Width, (int)e.NewSize.Height);
		}

		/// <summary>
		/// This method is used to set the dimensions and the viewport of the opengl control.
		/// </summary>
		/// <param name="width">The width of the OpenGL drawing area.</param>
		/// <param name="height">The height of the OpenGL drawing area.</param>
		private void UpdateOpenGLControl(int width, int height)
		{
			// Lock on OpenGL.
			lock (gl)
			{
				gl.SetDimensions(width, height);

				//	Set the viewport.
				gl.Viewport(0, 0, width, height);

				//  If we have a project handler, call it...
				if (width != -1 && height != -1)
				{
					RaiseEvent(new OpenGLRoutedEventArgs(ResizedEvent, gl));
				}
			}
			// Force re-creation of image buffer since size has changed
			m_imageBuffer = null;
		}

		/// <summary>
		/// When overridden in a derived class, is invoked whenever application code or 
		/// internal processes call <see cref="M:System.Windows.FrameworkElement.ApplyTemplate"/>.
		/// </summary>
		public override void OnApplyTemplate()
		{
			//  Call the base.
			base.OnApplyTemplate();

			//  Lock on OpenGL.
			lock (gl)
			{
				//  Create OpenGL.
				gl.Create(OpenGLVersion, RenderContextType, 1, 1, 32, null);
			}

			// Force re-set of dpi and format settings
			m_dpiX = 0;

			//  Set the most basic OpenGL styles.
			gl.ShadeModel(OpenGL.GL_SMOOTH);
			gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
			gl.ClearDepth(1.0f);
			gl.Enable(OpenGL.GL_DEPTH_TEST);
			gl.DepthFunc(OpenGL.GL_LEQUAL);
			gl.Hint(OpenGL.GL_PERSPECTIVE_CORRECTION_HINT, OpenGL.GL_NICEST);

			//  Fire the OpenGL initialised event.
			RaiseEvent(new OpenGLRoutedEventArgs(OpenGLInitializedEvent, gl));

			timer.Interval = new TimeSpan(0, 0, 0, 0, (int)(1000.0 / FrameRate));
		}

		/// <summary>
		/// Handles the Tick event of the timer control.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
		void timer_Tick(object sender, EventArgs e)
		{
			// If a manual render was not triggered, perform the scheduled one
			if (!stopwatch.IsRunning || stopwatch.ElapsedMilliseconds + 1 >= (int)(1000.0 / FrameRate)) DoRender();
		}

		/// <summary>
		/// Executes the GL Render
		/// </summary>
		public void DoRender()
		{
			// Discard render is OpenGL is not yet initialized
			if (gl == null || gl.RenderContextProvider == null) return;

			// Restart stopwatch to measure elapsed time since last rendered frame
			stopwatch.Reset();
			stopwatch.Start();

			//  Lock on OpenGL.
			lock (gl)
			{
				//  Make GL current.
				gl.MakeCurrent();

				//	If there is a draw handler, then call it.
				RaiseEvent(new OpenGLRoutedEventArgs(OpenGLDrawEvent, gl));

				//  Render.
				gl.Blit(IntPtr.Zero);

				switch (RenderContextType)
				{
					case RenderContextType.DIBSection:
						{
							var provider = gl.RenderContextProvider as DIBSectionRenderContextProvider;
							var hBitmap = provider.DIBSection.HBitmap;

							if (hBitmap != IntPtr.Zero)
							{
								FillImageSource(provider.DIBSection.Bits, hBitmap);
								// var newFormatedBitmapSource = GetFormatedBitmapSource(hBitmap);

								//  Copy the pixels over.
								// image.Source = newFormatedBitmapSource;
							}
						}
						break;
					case RenderContextType.NativeWindow:
						break;
					case RenderContextType.HiddenWindow:
						break;
					case RenderContextType.FBO:
						{
							var provider = gl.RenderContextProvider as FBORenderContextProvider;
							var hBitmap = provider.InternalDIBSection.HBitmap;

							if (hBitmap != IntPtr.Zero)
							{
								FillImageSource(provider.InternalDIBSection.Bits, hBitmap);
								// var newFormatedBitmapSource = GetFormatedBitmapSource(hBitmap);

								//  Copy the pixels over.
								// image.Source = newFormatedBitmapSource;
							}
						}
						break;
					default:
						break;
				}
			}
		}

		/// <summary>
		/// Fill the ImageSource from the provided bits IntPtr, using the provided hBitMap IntPtr
		/// if needed to determine key data from the bitmap source.
		/// </summary>
		/// <param name="bits">An IntPtr to the bits for the bitmap image.  Generally provided from
		/// DIBSectionRenderContextProvider.DIBSection.Bits or from
		/// FBORenderContextProvider.InternalDIBSection.Bits.</param>
		/// <param name="hBitmap">An IntPtr to the HBitmap for the image.  Generally provided from
		/// DIBSectionRenderContextProvider.DIBSection.HBitmap or from
		/// FBORenderContextProvider.InternalDIBSection.HBitmap.</param>
		public void FillImageSource(IntPtr bits, IntPtr hBitmap)
		{
			// If DPI hasn't been set, use a call to HBitmapToBitmapSource to fill the info
			// This should happen only ONCE (near the start of the application)
			if (m_dpiX == 0)
			{
				var bitmapSource = BitmapConversion.HBitmapToBitmapSource(hBitmap);
				m_dpiX = bitmapSource.DpiX;
				m_dpiY = bitmapSource.DpiY;
				m_format = bitmapSource.Format;
				m_bytesPerPixel = gl.RenderContextProvider.BitDepth >> 3;
				// FBO render context flips the image vertically, so transform to compensate
				if (RenderContextType == RenderContextType.FBO)
				{
					image.RenderTransform = new ScaleTransform(1.0, -1.0);
					image.RenderTransformOrigin = new Point(0.0, 0.5);
				}
				else
				{
					image.RenderTransform = Transform.Identity;
					image.RenderTransformOrigin = new Point(0.0, 0.0);
				}
			}
			// If the image buffer is null, create it
			// This should happen when the size of the image changes
			if (m_imageBuffer == null)
			{
				int width = gl.RenderContextProvider.Width;
				int height = gl.RenderContextProvider.Height;

				int imageBufferSize = width * height * m_bytesPerPixel;
				m_imageBuffer = new byte[imageBufferSize];
				m_writeableBitmap = new WriteableBitmap(width, height, m_dpiX, m_dpiY, m_format, null);
				m_imageRect = new Int32Rect(0, 0, width, height);
				m_imageStride = width * m_bytesPerPixel;
			}

			// Fill the image buffer from the bits and create the writeable bitmap
			System.Runtime.InteropServices.Marshal.Copy(bits, m_imageBuffer, 0, m_imageBuffer.Length);
			m_writeableBitmap.WritePixels(m_imageRect, m_imageBuffer, m_imageStride, 0);

			image.Source = m_writeableBitmap;
		}

		/// <summary>
		/// This method converts the output from the OpenGL render context provider to a 
		/// FormatConvertedBitmap in order to show it in the image.
		/// </summary>
		/// <param name="hBitmap">The handle of the bitmap from the OpenGL render context.</param>
		/// <returns>Returns the new format converted bitmap.</returns>
		private static FormatConvertedBitmap GetFormatedBitmapSource(IntPtr hBitmap)
		{
			//  TODO: We have to remove the alpha channel - for some reason it comes out as 0.0 
			//  meaning the drawing comes out transparent.

			FormatConvertedBitmap newFormatedBitmapSource = new FormatConvertedBitmap();
			newFormatedBitmapSource.BeginInit();
			newFormatedBitmapSource.Source = BitmapConversion.HBitmapToBitmapSource(hBitmap);
			newFormatedBitmapSource.DestinationFormat = PixelFormats.Rgb24;
			newFormatedBitmapSource.EndInit();

			return newFormatedBitmapSource;
		}

		/// <summary>
		/// Called when the frame rate is changed.
		/// </summary>
		/// <param name="o">The object.</param>
		/// <param name="args">The <see cref="System.Windows.DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
		private static void OnFrameRateChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
		{
			//  Get the control.
			OpenGLControlExt me = o as OpenGLControlExt;

			//  If we have the timer, set the time.
			if (me.timer != null)
			{
				//  Stop the timer.
				me.timer.Stop();

				//  Set the timer.
				me.timer.Interval = new TimeSpan(0, 0, 0, 0, (int)(1000f / me.FrameRate));

				//  Start the timer.
				me.timer.Start();
			}
		}

		/// <summary>
		/// The OpenGL instance.
		/// </summary>
		private OpenGL gl = new OpenGL();

		/// <summary>
		/// The dispatcher timer.
		/// </summary>
		DispatcherTimer timer = null;

		/// <summary>
		/// A stopwatch used for timing rendering.
		/// </summary>
		protected Stopwatch stopwatch = new Stopwatch();

		private static readonly RoutedEvent OpenGLInitializedEvent = EventManager.RegisterRoutedEvent("OpenGLInitialized",
			RoutingStrategy.Direct, typeof(OpenGLRoutedEventHandler), typeof(OpenGLControlExt));

		/// <summary>
		/// Occurs when OpenGL should be initialised.
		/// </summary>
		[Description("Called when OpenGL has been initialized."), Category("SharpGL")]
		public event OpenGLRoutedEventHandler OpenGLInitialized;

		private static readonly RoutedEvent OpenGLDrawEvent = EventManager.RegisterRoutedEvent("OpenGLDraw",
			RoutingStrategy.Direct, typeof(OpenGLRoutedEventHandler), typeof(OpenGLControlExt));

		/// <summary>
		/// Occurs when OpenGL drawing should occur.
		/// </summary>
		[Description("Called whenever OpenGL drawing should occur."), Category("SharpGL")]
		public event OpenGLRoutedEventHandler OpenGLDraw;

		private static readonly RoutedEvent ResizedEvent = EventManager.RegisterRoutedEvent("Resized",
			RoutingStrategy.Direct, typeof(OpenGLRoutedEventHandler), typeof(OpenGLControlExt));

		/// <summary>
		/// Occurs when the control is resized. This can be used to perform custom projections.
		/// </summary>
		[Description("Called when the control is resized - you can use this to do custom viewport projections."), Category("SharpGL")]
		public event OpenGLRoutedEventHandler Resized;

		/// <summary>
		/// The frame rate dependency property.
		/// </summary>
		private static readonly DependencyProperty FrameRateProperty =
		  DependencyProperty.Register("FrameRate", typeof(double), typeof(OpenGLControlExt),
		  new PropertyMetadata(28.0, new PropertyChangedCallback(OnFrameRateChanged)));

		/// <summary>
		/// Gets or sets the frame rate.
		/// </summary>
		/// <value>The frame rate.</value>
		public double FrameRate
		{
			get { return (double)GetValue(FrameRateProperty); }
			set { SetValue(FrameRateProperty, value); }
		}

		/// <summary>
		/// The render context type property.
		/// </summary>
		private static readonly DependencyProperty RenderContextTypeProperty =
		  DependencyProperty.Register("RenderContextType", typeof(RenderContextType), typeof(OpenGLControl),
		  new PropertyMetadata(RenderContextType.DIBSection, new PropertyChangedCallback(OnRenderContextTypeChanged)));

		/// <summary>
		/// Gets or sets the type of the render context.
		/// </summary>
		/// <value>The type of the render context.</value>
		public RenderContextType RenderContextType
		{
			get { return (RenderContextType)GetValue(RenderContextTypeProperty); }
			set { SetValue(RenderContextTypeProperty, value); }
		}

		/// <summary>
		/// Called when [render context type changed].
		/// </summary>
		/// <param name="o">The o.</param>
		/// <param name="args">The <see cref="System.Windows.DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
		private static void OnRenderContextTypeChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
		{
			OpenGLControlExt me = o as OpenGLControlExt;
		}

		/// <summary>
		/// The OpenGL Version property.
		/// </summary>
		private static readonly DependencyProperty OpenGLVersionProperty =
		  DependencyProperty.Register("OpenGLVersion", typeof(OpenGLVersion), typeof(OpenGLControlExt),
		  new PropertyMetadata(OpenGLVersion.OpenGL2_1));

		/// <summary>
		/// Gets or sets the OpenGL Version requested for the control.
		/// </summary>
		/// <value>The type of the render context.</value>
		public OpenGLVersion OpenGLVersion
		{
			get { return (OpenGLVersion)GetValue(OpenGLVersionProperty); }
			set { SetValue(OpenGLVersionProperty, value); }
		}

		/// <summary>
		/// The DrawFPS property.
		/// </summary>
		private static readonly DependencyProperty DrawFPSProperty =
		  DependencyProperty.Register("DrawFPS", typeof(bool), typeof(OpenGLControlExt),
		  new PropertyMetadata(false, null));

		/// <summary>
		/// Gets or sets a value indicating whether to draw FPS.
		/// </summary>
		/// <value>
		///   <c>true</c> if draw FPS; otherwise, <c>false</c>.
		/// </value>
		public bool DrawFPS
		{
			get { return (bool)GetValue(DrawFPSProperty); }
			set { SetValue(DrawFPSProperty, value); }
		}

		/// <summary>
		/// The Render trigger of this control
		/// </summary>
		public static readonly DependencyProperty RenderTriggerProperty =
			DependencyProperty.Register("RenderMode", typeof(RenderTrigger), typeof(OpenGLControlExt),
			new PropertyMetadata(RenderTrigger.TimerBased));

		/// <summary>
		/// Gets or sets the Render trigger of this control
		/// </summary>
		public RenderTrigger RenderTrigger
		{
			get { return (RenderTrigger)GetValue(RenderTriggerProperty); }
			set
			{
				SetValue(RenderTriggerProperty, value);
				if (value == RenderTrigger.TimerBased)
				{
					timer.Start();
				}
				else
				{
					timer.Stop();
				}
			}
		}

		/// <summary>
		/// Gets the OpenGL instance.
		/// </summary>
		public OpenGL OpenGL
		{
			get { return gl; }
		}
	}
}
