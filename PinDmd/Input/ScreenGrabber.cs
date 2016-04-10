using System;
using System.Drawing;
using System.Reactive.Linq;

namespace PinDmd.Input
{
	public class ScreenGrabber : IFrameSource
	{
		public double FramesPerSecond { get; set; } = 1;
		public int Left { get; set; }
		public int Top { get; set; }
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;

		public Bitmap CaptureImage()
		{
			Console.WriteLine("*click*");
			return NativeCapture.GetDesktopBitmap(Left, Top, Width, Height);
		}

		public IObservable<Bitmap> GetFrames()
		{
			return Observable
				.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
				.Select(x => CaptureImage());
		}
	}
}
