using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Common;

namespace LibDmd.Input.PinMame
{
	/// <summary>
	/// Receives RGB24 frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class VpmRgb24Source : AbstractSource, IRgb24Source
	{
		public override string Name { get; } = "VPM RGB24 Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<byte[]> _framesRgb24 = new Subject<byte[]>();
		private byte[] _lastFrame;

		public void NextFrame(int width, int height, byte[] frame)
		{
			if (_lastFrame != null && FrameUtil.CompareBuffers(frame, 0, _lastFrame, 0, frame.Length)) {
				// identical frame, drop.
				return;
			}
			if (_lastFrame?.Length != frame.Length) {
				_lastFrame = new byte[frame.Length];
			}
			SetDimensions(width, height);
			_framesRgb24.OnNext(frame);
			Buffer.BlockCopy(frame, 0, _lastFrame, 0, frame.Length);
		}

		public IObservable<byte[]> GetRgb24Frames()
		{
			return _framesRgb24;
		}
	}
}
