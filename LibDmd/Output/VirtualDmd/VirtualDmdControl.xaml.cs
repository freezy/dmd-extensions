using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;

namespace LibDmd.Output.VirtualDmd
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : IGray2Destination, IGray4Destination, IRgb24Destination, IBitmapDestination
	{
		public static readonly Color DefaultColor = Colors.OrangeRed;

		public readonly int DmdWidth = 128;
		public readonly int DmdHeight = 32;

		public bool IsAvailable { get; } = true;
		public bool IsRgb { get; } = true;
		public bool IgnoreAspectRatio {
			get { return Dmd.Stretch == Stretch.UniformToFill; }
			set { Dmd.Stretch = value ? Stretch.Fill : Stretch.UniformToFill; }
		}

		private double _hue;
		private double _sat;
		private double _lum;

		private Color[] _gray2Palette;
		private Color[] _gray4Palette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdControl()
		{
			InitializeComponent();
			ClearColor();
		}

		public void Render(BitmapSource bmp)
		{
			Dispatcher.Invoke(() => Dmd.Source = bmp);
		}

		public void RenderGray2(byte[] frame)
		{
			// retrieve dimensions from frame size with AR = 1:4
			if (_gray2Palette != null) {
				RenderRgb24(ColorUtil.ColorizeFrame(DmdWidth, DmdHeight, frame, _gray2Palette));
			} else {
				Render(ImageUtil.ConvertFromGray2(DmdWidth, DmdHeight, frame, _hue, _sat, _lum));
			}
		}

		public void RenderGray4(byte[] frame)
		{
			// retrieve dimensions from frame size with AR = 1:4
			if (_gray4Palette != null) {
				RenderRgb24(ColorUtil.ColorizeFrame(DmdWidth, DmdHeight, frame, _gray4Palette));
			} else {
				Render(ImageUtil.ConvertFromGray4(DmdWidth, DmdHeight, frame, _hue, _sat, _lum));
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			if (frame.Length % 3 != 0) {
				throw new ArgumentException("RGB24 buffer must be divisible by 3, but " + frame.Length + " isn't.");
			}
			Render(ImageUtil.ConvertFromRgb24(DmdWidth, DmdHeight, frame));
		}

		public void SetColor(Color color)
		{
			ColorUtil.RgbToHsl(color.R, color.G, color.B, out _hue, out _sat, out _lum);
		}

		public void SetPalette(Color[] colors)
		{
			//Array.ForEach(colors, c => Logger.Trace("   " + c));
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
			SetColor(DefaultColor);
		}

		public void Init()
		{
			// nothing to init
		}

		public void Dispose()
		{
			// nothing to dispose
		}
	}
}
