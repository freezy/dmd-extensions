using System;
using System.Drawing;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;

namespace LibDmd.Input.ScreenGrabber
{
	/// <summary>
	/// A screen grabber that captures a portion of the desktop given by 
	/// position and dimensions.
	/// </summary>
	public class ScreenGrabber : IFrameSource
	{
		public double FramesPerSecond { get; set; } = 15;
		public int Left { get; set; }
		public int Top { get; set; }
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;

		public IObservable<BitmapSource> GetFrames()
		{
			return Observable
				.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
				.Select(x => CaptureImage());
		}

		public void Move(Rectangle rect)
		{
			Left = rect.X;
			Top = rect.Y;
			Width = rect.Width;
			Height = rect.Height;
		}

		private BitmapSource CaptureImage()
		{
			return NativeCapture.GetDesktopBitmap(Left, Top, Width, Height);
		}
	}
}
