using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.PinMame
{
	/// <summary>
	/// Receives 4-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class VpmGray6Source : AbstractSource, IGray6Source
	{
		public override string Name { get; } = "VPM 6-bit Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DMDFrame> _framesGray6 = new Subject<DMDFrame>();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		public VpmGray6Source(BehaviorSubject<FrameFormat> lastFrameFormat)
		{
			_lastFrameFormat = lastFrameFormat;
		}

		public void NextFrame(DMDFrame frame)
		{
			SetDimensions(frame.width, frame.height);
			_framesGray6.OnNext(frame);
			_lastFrameFormat.OnNext(FrameFormat.Gray6);
		}

		public IObservable<DMDFrame> GetGray6Frames()
		{
			return _framesGray6;
		}
	}
}
