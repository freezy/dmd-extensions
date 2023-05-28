using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class SourceColoredGray2 : IColoredGray2Source, ITestSource<ColoredFrame>
	{
		public string Name => "Gray2 Test Source";

		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private readonly Subject<ColoredFrame> _frames = new Subject<ColoredFrame>();

		public IObservable<ColoredFrame> GetColoredGray2Frames() => _frames;

		public void AddFrame(ColoredFrame frame)
		{
			_frames.OnNext(frame);
		}
	}
}
