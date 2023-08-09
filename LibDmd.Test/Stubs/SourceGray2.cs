using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class SourceGray2 : IGray2Source, ITestSource<DmdFrame>
	{
		public string Name => "Source[Gray2]";

		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private readonly Subject<DmdFrame> _frames = new Subject<DmdFrame>();

		public IObservable<DmdFrame> GetGray2Frames(bool dedupe) => _frames;

		public void AddFrame(DmdFrame frame)
		{
			_frames.OnNext(frame);
		}
	}
}
