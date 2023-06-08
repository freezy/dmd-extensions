using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// Receives colored 2-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class PassthroughColoredGray2Source : AbstractSource, IColoredGray2Source
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<ColoredFrame> _frames = new Subject<ColoredFrame>();

		private readonly ColoredFrame _lastFrame = new ColoredFrame();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		public PassthroughColoredGray2Source(BehaviorSubject<FrameFormat> lastFrameFormat, string name)
		{
			_lastFrameFormat = lastFrameFormat;
			Name = name;
		}

		public void NextFrame(ColoredFrame frame)
		{
			// de-dupe frame
			if (_lastFrameFormat.Value == FrameFormat.ColoredGray2 && _lastFrame == frame) {
				return;
			}
			_lastFrame.Update(frame);
			_lastFrameFormat.OnNext(FrameFormat.ColoredGray2);
			_frames.OnNext(frame);
		}

		public IObservable<ColoredFrame> GetColoredGray2Frames() => _frames;
	}
}
