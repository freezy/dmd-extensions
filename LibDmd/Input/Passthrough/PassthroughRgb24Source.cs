using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Frame;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// Receives RGB24 frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class PassthroughRgb24Source : AbstractSource, IRgb24Source, IGameNameSource
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DmdFrame> _framesRgb24 = new Subject<DmdFrame>();
		private readonly ISubject<string> _gameName = new Subject<string>();

		private readonly bool _deDupe;
		private readonly DmdFrame _lastFrame = new DmdFrame();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		public PassthroughRgb24Source(BehaviorSubject<FrameFormat> lastFrameFormat = null,
			string name = "Passthrough RGB24 Source", bool deDupe = true)
		{
			if (deDupe && lastFrameFormat == null) {
				throw new ArgumentException("lastFrameFormat must be provided if deDupe is enabled.");
			}
			_deDupe = deDupe;
			_lastFrameFormat = lastFrameFormat;
			Name = name;
		}

		public void NextFrame(DmdFrame frame)
		{
			// de-dupe frame
			if (_deDupe && _lastFrameFormat.Value == FrameFormat.Rgb24 && _lastFrame == frame) {
				return;
			}

			_lastFrame.Update(frame);
			_lastFrameFormat.OnNext(FrameFormat.Rgb24);
			_framesRgb24.OnNext(frame);
		}

		public IObservable<DmdFrame> GetRgb24Frames() => _framesRgb24;

		public void NextGameName(string gameName) => _gameName.OnNext(gameName);

		public IObservable<string> GetGameName() => _gameName;
	}
}
