using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;

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

		public double DotSize
		{
			get { return _dotSize; }
			set { _dotSize = value; UpdateEffectParams(); }
		}

		public double DotGlow
		{
			get { return _dotGlow; }
			set { _dotGlow = value; UpdateEffectParams(); }
		}

		public double Brightness
		{
			get { return _brightness; }
			set { _brightness = value; UpdateEffectParams(); }
		}

		public double BackGlow
		{
			get { return _backGlow; }
			set { _backGlow = value; UpdateEffectParams(); }
		}

		public Color GlassColor
		{
			get { return _glassColor; }
			set { _glassColor = value; UpdateEffectParams(); }
		}

		public string Glass
		{
			get { return _glass; }
			set
			{
				_glass = value;
				try
				{
					_glassImage = new ImageBrush(new BitmapImage(new Uri(_glass)));
				}
				catch
				{
					_glassImage = null;
				}
				UpdateEffectParams();
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
		private double _dotSize = 1.0;
		private double _dotGlow = 0.0;
		private double _brightness = 1.0;
		private double _backGlow = 0.0;
		private string _glass = null;
		private Thickness _glassPad = new Thickness(0);
		private string _frame = null;
		private Thickness _framePad = new Thickness(0);
		private ImageBrush _glassImage = null;
		private Color _glassColor = Color.FromArgb(0, 0, 0, 0);
		private readonly EnhancedDmdEffect _fullEffect;
		private readonly BaseDmdEffect _baseEffect;
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;
		private BitmapSource[] _mipmap = new BitmapSource[5];

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdControl()
		{
			InitializeComponent();
			SizeChanged += OnSizeChanged;
			_fullEffect = ((EnhancedDmdEffect)FindResource("FullEffect"));
			_baseEffect = ((BaseDmdEffect)FindResource("BaseEffect"));
			ClearColor();
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			try
			{
				Dispatcher.Invoke(() =>
				{
					Dmd.Source = bmp;
					if (Dmd.Effect == _fullEffect)
					{
						var xScale = bmp.Width / DmdWidth;
						var yScale = bmp.Height / DmdHeight;
						int width = (int)(bmp.Width + xScale * (_glassPad.Left + _glassPad.Right));
						int height = (int)(bmp.Height + yScale * (_glassPad.Top + _glassPad.Bottom));
						if (width == bmp.Width && height == bmp.Height)
						{
							// No padding: create the mipmap directly from the DMD
							_mipmap[0] = bmp;
						}
						else
						{
							// Create the mipmap from a padded DMD to allow clean light leaking inside the padding area
							var rect = new Rect(xScale * _glassPad.Left, yScale * _glassPad.Top, bmp.Width, bmp.Height);
							var group = new DrawingGroup();
							group.Children.Add(new ImageDrawing(bmp, rect));
							var drawingVisual = new DrawingVisual();
							using (var drawingContext = drawingVisual.RenderOpen())
							{
								drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(0,0,0)), new Pen(), new Rect(0, 0, width, height));
								drawingContext.DrawDrawing(group);
							}
							if (_mipmap[0] == null || !(_mipmap[0] is RenderTargetBitmap) || _mipmap[0].Width != width || _mipmap[0].Height != height)
								_mipmap[0] = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Default);
							((RenderTargetBitmap)_mipmap[0]).Render(drawingVisual);
						}
						for (int i = 1; i < 5; i++)
						{
							width /= 2;
							height /= 2;
							var rect = new Rect(0, 0, width, height);
							var group = new DrawingGroup();
							RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.HighQuality);
							group.Children.Add(new ImageDrawing(_mipmap[i - 1], rect));
							var drawingVisual = new DrawingVisual();
							using (var drawingContext = drawingVisual.RenderOpen())
								drawingContext.DrawDrawing(group);
							if (_mipmap[i] == null || !(_mipmap[i] is RenderTargetBitmap) || _mipmap[i].Width != width || _mipmap[i].Height != height)
								_mipmap[i] = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Default);
							((RenderTargetBitmap)_mipmap[i]).Render(drawingVisual);
						}
						((ImageBrush)FindResource("DmdLevel1")).ImageSource = _mipmap[1];
						((ImageBrush)FindResource("DmdLevel2")).ImageSource = _mipmap[2];
						((ImageBrush)FindResource("DmdLevel3")).ImageSource = _mipmap[3];
						((ImageBrush)FindResource("DmdLevel4")).ImageSource = _mipmap[4];
					}
				});
			}
			catch (TaskCanceledException e)
			{
				Logger.Warn(e, "Virtual DMD renderer task seems to be lost.");
			}
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
			Logger.Info("Resizing virtual DMD to {0}x{1}", width, height);
			DmdWidth = width;
			DmdHeight = height;
			OnSizeChanged(null, null);
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
				UpdateEffectParams();
			});
		}

		private void UpdateEffectParams()
		{
			Dispatcher.Invoke(() =>
			{
				if (DotGlow == 0 && BackGlow == 0
					&& (Glass == null || Glass.Length == 0) && (GlassColor.R == 0 && GlassColor.G == 0 && GlassColor.B == 0 && GlassColor.A == 0)
					&& _glassPad.Left == 0 && _glassPad.Right == 0 && _glassPad.Top == 0 && _glassPad.Bottom == 0)
				{
					if (Dmd.Effect != _baseEffect)
					{
						Logger.Info("Virtual DMD switching to base shader.");
						Dmd.Effect = _baseEffect;
					}
					_baseEffect.Size = new Point(DmdWidth, DmdHeight);
					_baseEffect.DotSize = DotSize;
					_baseEffect.Brightness = Brightness;
				}
				else
				{
					if (Dmd.Effect != _fullEffect)
					{
						Logger.Info("Virtual DMD switching to enhanced shader.");
						Dmd.Effect = _fullEffect;
					}
					_fullEffect.Size = new Point(DmdWidth, DmdHeight);
					_fullEffect.DotSize = DotSize;
					_fullEffect.DotGlow = DotGlow;
					_fullEffect.BackGlow = BackGlow;
					_fullEffect.Brightness = Brightness;
					_fullEffect.Glass = _glassImage;
					_fullEffect.GlassColor = GlassColor;
					_fullEffect.GlassTexOffset = new Point(_glassPad.Left / DmdWidth, _glassPad.Top / DmdHeight);
					_fullEffect.GlassTexScale = new Point(1f + (_glassPad.Left + _glassPad.Right) / DmdWidth, 1f + (_glassPad.Top + _glassPad.Bottom) / DmdHeight);
				}
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
