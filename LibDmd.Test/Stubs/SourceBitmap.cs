using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class SourceBitmap : IBitmapSource, ITestSource<BmpFrame>
	{
		public string Name => "Bitmap Test Source";

		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private readonly Subject<BmpFrame> _frames = new Subject<BmpFrame>();

		public IObservable<BmpFrame> GetBitmapFrames() => _frames;

		public void AddFrame(BmpFrame frame)
		{
			_frames.OnNext(frame);
		}
	}
}
