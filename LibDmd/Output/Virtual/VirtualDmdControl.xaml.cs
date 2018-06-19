using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;

namespace LibDmd.Output.Virtual
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : IRgb24Destination, IBitmapDestination, IResizableDestination
	// these others are for debugging purpose. basically you can make the virtual dmd 
	// behave like any other display by adding/removing interfaces
	// standard (aka production); IRgb24Destination, IBitmapDestination, IResizableDestination
	// pindmd1/2: IGray2Destination, IGray4Destination, IResizableDestination, IFixedSizeDestination
	// pin2dmd: IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IFixedSizeDestination
	// pindmd3: IGray2Destination, IGray4Destination, IColoredGray2Destination, IFixedSizeDestination
	{
		public int DmdWidth { get; private set; } = 128;
		public int DmdHeight { get; private set; } = 32;

		public bool IsAvailable { get; } = true;

		public double DotSize {
			get { return _dotSize; }
			set { _dotSize = value; UpdateEffectParams(); }
		}

		public IDmdWindow Host { set; get; }
		public bool IgnoreAspectRatio {
			get { return Dmd.Stretch == Stretch.UniformToFill; }
			set { Dmd.Stretch = value ? Stretch.Fill : Stretch.UniformToFill; }
		}

		private double _hue;
		private double _sat;
		private double _lum;

		private double _dotSize = 1.0;
		private Color[] _gray2Palette;
		private Color[] _gray4Palette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdControl()
		{
			InitializeComponent();
			ClearColor();
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			try {
				Dispatcher.Invoke(() => Dmd.Source = bmp);
			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Virtual DMD renderer task seems to be lost.");
			}
		}

		public void RenderGray2(byte[] frame)
		{
			if (_gray2Palette != null) {
				RenderRgb24(ColorUtil.ColorizeFrame(DmdWidth, DmdHeight, frame, _gray2Palette));
			} else {
				RenderBitmap(ImageUtil.ConvertFromGray2(DmdWidth, DmdHeight, frame, _hue, _sat, _lum));
			}
		}

		public void RenderGray4(byte[] frame)
		{
			if (_gray4Palette != null) {
				RenderRgb24(ColorUtil.ColorizeFrame(DmdWidth, DmdHeight, frame, _gray4Palette));
			} else {
				RenderBitmap(ImageUtil.ConvertFromGray4(DmdWidth, DmdHeight, frame, _hue, _sat, _lum));
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			if (frame.Length % 3 != 0) {
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
			UpdateEffectParams();
			Host.SetDimensions(width, height);
		}

		private void UpdateEffectParams()
		{
			Dispatcher.Invoke(() => {
				Effect.AspectRatio = (double)DmdWidth / DmdHeight;
				Effect.BlockCount = Math.Max(DmdWidth, DmdHeight);
				Effect.Max = Effect.AspectRatio * 0.47 * _dotSize;
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
			if (this is IFixedSizeDestination) {
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

	public interface IDmdWindow
	{
		void SetDimensions(int width, int height);
	}
}
