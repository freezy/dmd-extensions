using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class SourceRgb24 : IRgb24Source, ITestSource<DmdFrame>
	{
		public string Name => "Source[Rgb24]";

		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private readonly Subject<DmdFrame> _frames = new Subject<DmdFrame>();

		public IObservable<DmdFrame> GetRgb24Frames() => _frames;

		public void AddFrame(DmdFrame frame)
		{
			_frames.OnNext(frame);
		}
	}
}
