using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Processor;

namespace LibDmd.Input.ScreenGrabber
{
	/// <summary>
	/// A screen grabber that captures a portion of the desktop given by 
	/// position and dimensions.
	/// </summary>
	public class ScreenGrabber : AbstractSource, IBitmapSource
	{
		public override string Name { get; } = "Screen Grabber";

		public readonly List<AbstractProcessor> Processors = new List<AbstractProcessor>();
		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		public double FramesPerSecond { get; set; } = 30;
		public int Left { get; set; }
		public int Top { get; set; }
		public int Width = 128;
		public int Height = 32;
		public int DestinationWidth
		{
			get { return _destWidth; }
			set {
				_destWidth = value;
				SetDimensions(_destWidth, _destHeight);
			}
		}
		public int DestinationHeight {
			get { return _destHeight; }
			set {
				_destHeight = value;
				SetDimensions(_destWidth, _destHeight);
			}
		}

		private int _destWidth = 128;
		private int _destHeight = 32;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private IObservable<BitmapSource> _frames;

		public IObservable<BitmapSource> GetBitmapFrames()
		{
			var enabledProcessors = Processors.Where(processor => processor.Enabled);
			return _frames ?? (
				_frames = Observable
					.Interval(TimeSpan.FromMilliseconds(1000 / FramesPerSecond))
					.Select(x => CaptureImage())
					.Select(bmp => enabledProcessors.Aggregate(bmp, (currentBmp, processor) => processor.Process(currentBmp)))
					.Select(bmp => {
						if (DestinationHeight > 0 && DestinationWidth > 0) {
							return TransformationUtil.Transform(bmp, DestinationWidth, DestinationHeight, ResizeMode.Stretch, false, false);
						}
						return bmp;
					})
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
			SetDimensions(rect.Width, rect.Height);
		}

		private BitmapSource CaptureImage()
		{
			return NativeCapture.GetDesktopBitmap(Left, Top, Width, Height);
		}
	}
}
