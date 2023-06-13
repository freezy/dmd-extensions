using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// Receives colored 6-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class PassthroughColoredGray6Source : AbstractSource, IColoredGray6Source
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<ColoredFrame> _frames = new Subject<ColoredFrame>();

		private readonly bool _dedupe;
		private readonly ColoredFrame _lastFrame = new ColoredFrame();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		public PassthroughColoredGray6Source(BehaviorSubject<FrameFormat> lastFrameFormat,
			string name = "Passthrough Colored Gray6 Source", bool dedupe = true)
		{
			if (dedupe && lastFrameFormat == null) {
				throw new ArgumentException("lastFrameFormat must be provided if deDupe is enabled.");
			}
			_dedupe = dedupe;
			_lastFrameFormat = lastFrameFormat;
			Name = name;
		}

		public void NextFrame(ColoredFrame frame)
		{
			// de-dupe frame
			if (_dedupe && _lastFrameFormat.Value == FrameFormat.ColoredGray6 && _lastFrame == frame) {
				return;
			}
			_lastFrame.Update(frame);
			_lastFrameFormat.OnNext(FrameFormat.ColoredGray6);
			_frames.OnNext(frame);
		}

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _frames;
	}
}
