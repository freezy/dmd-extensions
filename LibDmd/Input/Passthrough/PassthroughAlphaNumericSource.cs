using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// Receives alphanumeric frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class PassthroughAlphaNumericSource : AbstractSource, IAlphaNumericSource, IGameNameSource
	{
		public override string Name { get; } = "VPM Alpha Numeric Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;
		
		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly ISubject<AlphaNumericFrame> _framesAlphaNumeric;
		private readonly ISubject<string> _gameName = new Subject<string>();

		private readonly AlphaNumericFrame _lastFrame = new AlphaNumericFrame();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		public PassthroughAlphaNumericSource(AlphaNumericFrame initialFrame)
		{
			_framesAlphaNumeric = new BehaviorSubject<AlphaNumericFrame>(initialFrame);
		}

		public PassthroughAlphaNumericSource(BehaviorSubject<FrameFormat> lastFrameFormat)
		{
			_framesAlphaNumeric =  new Subject<AlphaNumericFrame>();
			_lastFrameFormat = lastFrameFormat;
		}

		public void NextFrame(AlphaNumericFrame frame)
		{
			// de-dupe frame
			if (_lastFrameFormat.Value == FrameFormat.AlphaNumeric && _lastFrame == frame) {
				return;
			}

			_lastFrame.Update(frame);
			_framesAlphaNumeric.OnNext(frame);
			_lastFrameFormat.OnNext(FrameFormat.AlphaNumeric);
		}

		public void NextGameName(string gameName) => _gameName.OnNext(gameName);

		public IObservable<AlphaNumericFrame> GetAlphaNumericFrames() => _framesAlphaNumeric;

		public IObservable<string> GetGameName() => _gameName;
	}
}
