using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// Receives 8-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates (if enabled).
	/// </summary>
	public class PassthroughGray8Source : AbstractSource, IGray8Source, IGameNameSource
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DmdFrame> _framesGray8Deduped = new Subject<DmdFrame>();
		private readonly Subject<DmdFrame> _framesGray8Duped = new Subject<DmdFrame>();
		private readonly ISubject<string> _gameName = new Subject<string>();

		private readonly DmdFrame _lastFrame = new DmdFrame();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PassthroughGray8Source(BehaviorSubject<FrameFormat> lastFrameFormat = null,
			string name = "Passthrough Gray8 Source")
		{
			_lastFrameFormat = lastFrameFormat;
			Name = name;
		}

		public void NextFrame(DmdFrame frame)
		{
			_framesGray8Duped.OnNext(frame);

			// de-dupe frame
			if (_lastFrameFormat.Value == FrameFormat.Gray8 && _lastFrame == frame) {
				return;
			}

			_lastFrame.Update(frame);
			_lastFrameFormat.OnNext(FrameFormat.Gray8);
			_framesGray8Deduped.OnNext(frame);
		}

		public IObservable<DmdFrame> GetGray8Frames(bool dedupe) => dedupe ? _framesGray8Deduped : _framesGray8Duped;

		public void NextGameName(string gameName) => _gameName.OnNext(gameName);

		public IObservable<string> GetGameName() => _gameName;
	}
}
