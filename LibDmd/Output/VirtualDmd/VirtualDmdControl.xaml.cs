using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;

namespace LibDmd.Output.VirtualDmd
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : UserControl, IFrameDestination, IGray4
	{
		public bool IsAvailable { get; } = true;
		public bool IsRgb { get; } = true;

		public VirtualDmdControl()
		{
			InitializeComponent();
		}

		public void Render(BitmapSource bmp)
		{
			Dispatcher.Invoke(() => Dmd.Source = bmp);
		}

		public void RenderGray4(BitmapSource bmp)
		{
			Render(bmp);
		}

		public void RenderGray4(byte[] frame)
		{
			// retrieve dimensions from frame size with AR = 1:4
			var width = 2 * (int) Math.Sqrt(frame.Length);
			var height = width / 4;
			var bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
			var bufferSize = (Math.Abs(bmp.BackBufferStride) * height + 2);
			var frameBuffer = new byte[bufferSize];

			var index = 0;
			bmp.Lock();
			for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x++) {

					// generate a "rainbow" pixel
					var pixelLum = frame[y * width + x]; // 0 - 15
					var lum = (double)pixelLum / 15;
					var hue = 0;
					//var lum = (double)pixelLum / 15 / 3 + 0.3;
					//var hue = (double)pixelLum / 15 * 6;
					byte red, green, blue;
					ColorUtil.HslToRgb(hue, 1, lum, out red, out green, out blue);

					frameBuffer[index] = blue;
					frameBuffer[index + 1] = green;
					frameBuffer[index + 2] = red;

					index += 4;
				}
			}
			bmp.WritePixels(new Int32Rect(0, 0, width, height), frameBuffer, bmp.BackBufferStride, 0);
			bmp.Unlock();
			bmp.Freeze();
			Render(bmp);
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
