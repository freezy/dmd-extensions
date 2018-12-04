using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
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
using SkiaSharp;

namespace LibDmd.Output.Virtual
{
	/// <summary>
	/// Interaction logic for AlphanumericControl.xaml
	/// </summary>
	public partial class AlphanumericControl : UserControl, IVirtualControl
	{
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool IsAvailable => true;

		public DisplaySetting DisplaySetting { get; set; }
		public bool IgnoreAspectRatio { get; set; }
		public VirtualWindow Host { get; set; }

		private WriteableBitmap _writeableBitmap;

		public AlphanumericControl()
		{
			DataContext = this;
			InitializeComponent();
			DisplaySetting = new DisplaySetting {
				Style = new RasterizeStyle {
					SkewAngle = -12,
					Background = new RasterizeLayerStyle { Color = new SKColor(0xff, 0xff, 0xff, 0x20), Blur = new SKPoint(7, 7), IsEnabled = true, IsBlurEnabled = true, IsDilateEnabled = false },
					OuterGlow = new RasterizeLayerStyle { Color = new SKColor(0xb6, 0x58, 0x29, 0x40), Blur = new SKPoint(50, 50), Dilate = new SKPoint(90, 40), IsEnabled = true, IsBlurEnabled = true, IsDilateEnabled = true },
					InnerGlow = new RasterizeLayerStyle { Color = new SKColor(0xdd, 0x6a, 0x03, 0xa0), Blur = new SKPoint(15, 13), Dilate = new SKPoint(15, 10), IsEnabled = true, IsBlurEnabled = true, IsDilateEnabled = true },
					Foreground = new RasterizeLayerStyle { Color = new SKColor(0xfb, 0xe6, 0xcb, 0xff), Blur = new SKPoint(2, 2), IsEnabled = true, IsBlurEnabled = true, IsDilateEnabled = false },
				}
			};

			SizeChanged += SizeChanged_Event;
			CompositionTarget.Rendering += (o, e) => DrawImage(_writeableBitmap);
		}

		public void ClearDisplay()
		{
			throw new NotImplementedException();
		}

		public void RenderSegments(ushort[] data)
		{
			UpdateData(data);
		}

		private void SizeChanged_Event(object sender, SizeChangedEventArgs e)
		{
			if (!Host.Resizing) {
				CreateImage((int)e.NewSize.Width, (int)e.NewSize.Height);
			}
		}

		private void SetBitmap(WriteableBitmap bitmap)
		{
			AlphanumericDisplay.Source = _writeableBitmap = bitmap;
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}
