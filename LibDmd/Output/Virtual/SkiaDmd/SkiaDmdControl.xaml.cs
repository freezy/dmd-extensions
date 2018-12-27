using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LibDmd.Common;
using NLog;

namespace LibDmd.Output.Virtual.SkiaDmd
{
	/// <summary>
	/// Interaction logic for SkiaDmdControl.xaml
	/// </summary>
	public partial class SkiaDmdControl : IRgb24Destination, IBitmapDestination, IResizableDestination, IVirtualControl
	{
		public bool IgnoreAspectRatio { get; set; }
		public VirtualDisplay Host { get; set; }
		public bool IsAvailable => true;

		public int DmdWidth { get; private set; } = 128;
		public int DmdHeight { get; private set; } = 32;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private double _hue;
		private double _sat;
		private double _lum;

		private Color[] _gray2Palette;
		private Color[] _gray4Palette;

		public SkiaDmdControl()
		{
			InitializeComponent();
		}

		public void Init()
		{
			// ReSharper disable once SuspiciousTypeConversion.Global
			if (this is IFixedSizeDestination) {
				SetDimensions(DmdWidth, DmdHeight);
			}
		}

		public void SetDimensions(int width, int height)
		{
			Logger.Info("Resizing Skia DMD to {0}x{1}", width, height);
			DmdWidth = width;
			DmdHeight = height;
			Host.SetDimensions(width, height);
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			try {
				Dispatcher.Invoke(() => DmdImage.Source = bmp);
			} catch (TaskCanceledException e) {
				Logger.Warn(e, "Virtual DMD renderer task seems to be lost.");
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			throw new NotImplementedException();
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

		public void ClearDisplay()
		{
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

		public void Dispose()
		{
		}
	}
}
