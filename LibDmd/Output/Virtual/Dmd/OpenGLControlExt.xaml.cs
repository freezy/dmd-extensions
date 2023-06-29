using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NLog;
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
		private byte[] _imageBuffer;
		private WriteableBitmap _writeableBitmap;
		private Int32Rect _imageRect;
		private int _imageStride;
		private double _dpiX;
		private double _dpiY;
		private PixelFormat _format;
		private int _bytesPerPixel;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
			// Force re-creation of image buffer since size has changed
			_imageBuffer = null;
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

			// Force a render on both buffers
			Dispatcher.Invoke(() => {
				DoRender();
				DoRender();
			});
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
			_dpiX = 0;

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

			// Force a render on both buffers
			Dispatcher.Invoke(() => {
				DoRender();
				DoRender();
			});
		}

		/// <summary>
		/// Handles the Tick event of the timer control.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
		void timer_Tick(object sender, EventArgs e)
		{
			DoRender();
		}

		public void RequestRender()
		{
			try{ 
				Dispatcher.Invoke(() => DoRender());

			} catch (TaskCanceledException e) {
				Logger.Error(e, "Main thread seems already destroyed, aborting.");
			}
		}

		/// <summary>
		/// Executes the GL Render
		/// </summary>
		public void DoRender()
		{
			// Discard render if OpenGL is not yet initialized
			if (gl == null || gl.RenderContextProvider == null) return;
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
			if (_dpiX == 0)
			{
				var bitmapSource = BitmapConversion.HBitmapToBitmapSource(hBitmap);
				_dpiX = bitmapSource.DpiX;
				_dpiY = bitmapSource.DpiY;
				_format = bitmapSource.Format;
				_bytesPerPixel = gl.RenderContextProvider.BitDepth >> 3;
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
			if (_imageBuffer == null)
			{
				int width = gl.RenderContextProvider.Width;
				int height = gl.RenderContextProvider.Height;

				int imageBufferSize = width * height * _bytesPerPixel;
				_imageBuffer = new byte[imageBufferSize];
				_writeableBitmap = new WriteableBitmap(width, height, _dpiX, _dpiY, _format, null);
				_imageRect = new Int32Rect(0, 0, width, height);
				_imageStride = width * _bytesPerPixel;
			}

			// Fill the image buffer from the bits and update the writeable bitmap
			System.Runtime.InteropServices.Marshal.Copy(bits, _imageBuffer, 0, _imageBuffer.Length);
			// FIXME Remove transparency
			// for (int i = 3; i < _imageBuffer.Length; i+=4) _imageBuffer[i] = 255;
			_writeableBitmap.WritePixels(_imageRect, _imageBuffer, _imageStride, 0);

			image.Source = _writeableBitmap;
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
		DispatcherTimer timer;

		private static readonly RoutedEvent OpenGLInitializedEvent = EventManager.RegisterRoutedEvent("OpenGLInitialized",
			RoutingStrategy.Direct, typeof(OpenGLRoutedEventHandler), typeof(OpenGLControlExt));

		/// <summary>
		/// Occurs when OpenGL should be initialised.
		/// </summary>
		[Description("Called when OpenGL has been initialized."), Category("SharpGL")]
#pragma warning disable CS0067
		public event OpenGLRoutedEventHandler OpenGLInitialized;
#pragma warning restore CS0067

		private static readonly RoutedEvent OpenGLDrawEvent = EventManager.RegisterRoutedEvent("OpenGLDraw",
			RoutingStrategy.Direct, typeof(OpenGLRoutedEventHandler), typeof(OpenGLControlExt));

		/// <summary>
		/// Occurs when OpenGL drawing should occur.
		/// </summary>
		[Description("Called whenever OpenGL drawing should occur."), Category("SharpGL")]
#pragma warning disable CS0067
		public event OpenGLRoutedEventHandler OpenGLDraw;
#pragma warning restore CS0067

		private static readonly RoutedEvent ResizedEvent = EventManager.RegisterRoutedEvent("Resized",
			RoutingStrategy.Direct, typeof(OpenGLRoutedEventHandler), typeof(OpenGLControlExt));

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
		  DependencyProperty.Register("RenderContextType", typeof(RenderContextType), typeof(OpenGLControlExt),
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
