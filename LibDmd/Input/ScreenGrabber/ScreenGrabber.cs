using System;
using System.Drawing;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using NLog;

namespace LibDmd.Input.ScreenGrabber
{
	/// <summary>
	/// A screen grabber that captures a portion of the desktop given by 
	/// position and dimensions.
	/// </summary>
	public class ScreenGrabber : AbstractSource, IBitmapSource
	{
		public override string Name { get; } = "Screen Grabber";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		public double FramesPerSecond { get; set; } = 15;
		public int Left { get; set; }
		public int Top { get; set; }
		public new int Width { get; set; } = 128;
		public new int Height { get; set; } = 32;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private IObservable<BitmapSource> _frames;

		public IObservable<BitmapSource> GetBitmapFrames()
		{
			return _frames ?? (
				_frames = Observable
					.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
					.Select(x => CaptureImage())
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
