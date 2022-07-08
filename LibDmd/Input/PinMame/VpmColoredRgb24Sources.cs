using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Common;

namespace LibDmd.Input.PinMame
{
	/// <summary>
	/// Receives colored RGB24 frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class VpmColoredRgb24Source : AbstractSource, IColoredRgb24Source
	{
		public override string Name { get; } = "VPM Colored RGB24 Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DMDFrame> _coloredFramesRgb24 = new Subject<DMDFrame>();
		private byte[] _lastFrame;
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		public VpmColoredRgb24Source(BehaviorSubject<FrameFormat> lastFrameFormat)
		{
			_lastFrameFormat = lastFrameFormat;
		}

		public void NextFrame(DMDFrame frame)
		{
			if (_lastFrameFormat.Value == FrameFormat.ColoredRgb24 && _lastFrame != null && FrameUtil.CompareBuffers(frame.Data, 0, _lastFrame, 0, frame.Data.Length)) {
				// identical frame, drop.
				return;
			}
			if (_lastFrame?.Length != frame.Data.Length) {
				_lastFrame = new byte[frame.Data.Length];
			}
			SetDimensions(frame.width, frame.height);
			_coloredFramesRgb24.OnNext(frame);
			Buffer.BlockCopy(frame.Data, 0, _lastFrame, 0, frame.Data.Length);
			_lastFrameFormat.OnNext(FrameFormat.ColoredRgb24);
		}

		public IObservable<DMDFrame> GetColoredRgb24Frames()
		{
			return _coloredFramesRgb24;
		}
	}
}
