using System;
using System.Drawing;
using System.Reactive.Linq;

namespace PinDmd.Input
{
	/// <summary>
	/// A screen grabber that captures a portion of the desktop given by 
	/// position and dimensions.
	/// </summary>
	public class ScreenGrabber : IFrameSource
	{
		public double FramesPerSecond { get; set; } = 1;
		public int Left { get; set; }
		public int Top { get; set; }
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;

		public IObservable<Bitmap> GetFrames()
		{
			return Observable
				.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
				.Select(x => CaptureImage());
		}

		private Bitmap CaptureImage()
		{
			Console.WriteLine("*click*");
			return NativeCapture.GetDesktopBitmap(Left, Top, Width, Height);
		}
	}
}
