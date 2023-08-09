using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// Receives 4-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates (if enabled).
	/// </summary>
	public class PassthroughGray4Source : AbstractSource, IGray4Source, IGameNameSource
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DmdFrame> _framesGray4Deduped = new Subject<DmdFrame>();
		private readonly Subject<DmdFrame> _framesGray4Duped = new Subject<DmdFrame>();
		private readonly ISubject<string> _gameName = new Subject<string>();

		private readonly DmdFrame _lastFrame = new DmdFrame();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PassthroughGray4Source(BehaviorSubject<FrameFormat> lastFrameFormat = null,
			string name = "Passthrough Gray4 Source")
		{
			_lastFrameFormat = lastFrameFormat;
			Name = name;
		}

		public void NextFrame(DmdFrame frame)
		{
			_framesGray4Duped.OnNext(frame);

			// de-dupe frame
			if (_lastFrameFormat.Value == FrameFormat.Gray4 && _lastFrame == frame) {
				return;
			}

			_lastFrame.Update(frame);
			_lastFrameFormat.OnNext(FrameFormat.Gray4);
			_framesGray4Deduped.OnNext(frame);
		}

		public IObservable<DmdFrame> GetGray4Frames(bool dedupe) => dedupe ? _framesGray4Deduped : _framesGray4Duped;

		public void NextGameName(string gameName) => _gameName.OnNext(gameName);

		public IObservable<string> GetGameName() => _gameName;
	}
}
