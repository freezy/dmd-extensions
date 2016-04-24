using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Video.FFMPEG;
using LibDmd.Input.ScreenGrabber;

namespace LibDmd.Input.Media
{
	public class MediaPlayer : IFrameSource
	{
		public string Name { get; } = "Media Player";

		public string Filename { get; set; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private IObservable<BitmapSource> _frames;

		public IObservable<BitmapSource> GetFrames()
		{
			// create instance of video reader
			VideoFileReader reader = new VideoFileReader();
			// open video file
			reader.Open(Filename);
			// check some of its attributes
			Console.WriteLine("width:  " + reader.Width);
			Console.WriteLine("height: " + reader.Height);
			Console.WriteLine("fps:    " + reader.FrameRate);
			Console.WriteLine("codec:  " + reader.CodecName);
			// read 100 video frames out of it
			
			//reader.Close();
			return _frames ?? (
				_frames = Observable

					.Interval(TimeSpan.FromMilliseconds(1000d / reader.FrameRate))
					.Take(100)
					.Select(x => Convert(reader.ReadVideoFrame()))
					.Publish()
					.RefCount()
			);
		}

		private BitmapSource CaptureImage()
		{
			return null;
		}

		public static BitmapSource Convert(Bitmap bitmap)
		{
			var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
			var bitmapSource = BitmapSource.Create(
				bitmapData.Width, bitmapData.Height, 96, 96, PixelFormats.Bgr32, null,
				bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

			bitmap.UnlockBits(bitmapData);
			bitmap.Dispose();
			bitmapSource.Freeze(); // make it readable on any thread
			return bitmapSource;
		}
	}
}
