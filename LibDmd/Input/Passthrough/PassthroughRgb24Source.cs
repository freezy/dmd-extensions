using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Common;

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

		private readonly Subject<DMDFrame> _framesRgb24 = new Subject<DMDFrame>();
		private readonly ISubject<string> _gameName = new Subject<string>();
		private byte[] _lastFrame;
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		public PassthroughRgb24Source(BehaviorSubject<FrameFormat> lastFrameFormat, string name)
		{
			_lastFrameFormat = lastFrameFormat;
			Name = name;
		}

		public void NextFrame(DMDFrame frame)
		{
			if (_lastFrameFormat.Value == FrameFormat.Rgb24 && _lastFrame != null && FrameUtil.CompareBuffers(frame.Data, 0, _lastFrame, 0, frame.Data.Length)) {
				// identical frame, drop.
				return;
			}
			if (_lastFrame?.Length != frame.Data.Length) {
				_lastFrame = new byte[frame.Data.Length];
			}
			SetDimensions(frame.Width, frame.Height);
			_framesRgb24.OnNext(frame);
			Buffer.BlockCopy(frame.Data, 0, _lastFrame, 0, frame.Data.Length);
			_lastFrameFormat.OnNext(FrameFormat.Rgb24);
		}

		public IObservable<DMDFrame> GetRgb24Frames() => _framesRgb24;

		public void NextGameName(string gameName) => _gameName.OnNext(gameName);

		public IObservable<string> GetGameName() => _gameName;
	}
}
