using System;
using System.Drawing;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using NLog;

namespace LibDmd.Input.ScreenGrabber
{
	/// <summary>
	/// A screen grabber that captures a portion of the desktop given by 
	/// position and dimensions.
	/// </summary>
	public class ScreenGrabber : BaseFrameSource
	{
		public override string Name { get; } = "Screen Grabber";

		public double FramesPerSecond { get; set; } = 15;
		public int Left { get; set; }
		public int Top { get; set; }
		public int Width { get; set; } = 128;
		public int Height { get; set; } = 32;

		private IObservable<Frame> _frames;

		public override IObservable<Frame> GetFrames()
		{
			return _frames ?? (
				_frames = Observable
					.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
					.Select(x => new Frame())
					.Select(frame => frame.SetBitmap(CaptureImage()))
					.Publish()
					.RefCount()
			);
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
