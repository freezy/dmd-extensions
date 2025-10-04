using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// Receives 2-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates (if enabled).
	/// </summary>
	public class PassthroughGray2Source : AbstractSource, IGray2Source, IGameNameSource
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DmdFrame> _framesGray2Duped = new Subject<DmdFrame>();
		private readonly Subject<DmdFrame> _framesGray2Deduped = new Subject<DmdFrame>();
		private readonly ISubject<string> _gameName = new Subject<string>();

		private readonly DmdFrame _lastFrame = new DmdFrame();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PassthroughGray2Source(BehaviorSubject<FrameFormat> lastFrameFormat = null,
			string name = "Passthrough Gray2 Source")
		{
			_lastFrameFormat = lastFrameFormat;
			Name = name;
		}

		public void NextFrame(DmdFrame frame)
		{
			_framesGray2Duped.OnNext(frame);

			// de-dupe frame
			if (_lastFrameFormat.Value == FrameFormat.Gray2 && _lastFrame == frame) {
				return;
			}

			_lastFrame.Update(frame);
			_lastFrameFormat.OnNext(FrameFormat.Gray2);
			_framesGray2Deduped.OnNext(frame);
		}

		public IObservable<DmdFrame> GetGray2Frames(bool dedupe, bool skipIdentificationFrames) => dedupe
			? (skipIdentificationFrames ? _framesGray2Deduped.Where(f => !f.IsIdentifyFrame) : _framesGray2Deduped)
			: (skipIdentificationFrames ? _framesGray2Duped.Where(f => !f.IsIdentifyFrame) : _framesGray2Duped);

		public void NextGameName(string gameName) => _gameName.OnNext(gameName);

		public IObservable<string> GetGameName() => _gameName;
	}
}
