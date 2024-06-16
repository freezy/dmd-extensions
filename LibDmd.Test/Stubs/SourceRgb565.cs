using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class SourceRgb565 : IRgb565Source, ITestSource<DmdFrame>
	{
		public string Name => "Source[Rgb565]";

		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private readonly Subject<DmdFrame> _frames = new Subject<DmdFrame>();

		public IObservable<DmdFrame> GetRgb565Frames() => _frames;

		public void AddFrame(DmdFrame frame)
		{
			_frames.OnNext(frame);
		}
	}
}
