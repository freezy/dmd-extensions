using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Common;
using NLog.Targets;

namespace LibDmd.Input.PinMame
{
	/// <summary>
	/// Receives 2-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class VpmGray2Source : AbstractSource, IGray2Source
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<byte[]> _framesGray2 = new Subject<byte[]>();
		private byte[] _lastFrame;

		public VpmGray2Source(string name)
		{
			Name = name;
		}

		public void NextFrame(byte[] frame)
		{
			if (_lastFrame != null && FrameUtil.CompareBuffers(frame, 0, _lastFrame, 0, frame.Length)) {
				// identical frame, skip.
				return;
			}
			if (_lastFrame?.Length != frame.Length) {
				_lastFrame = new byte[frame.Length];
			}
			_framesGray2.OnNext(frame);
			Buffer.BlockCopy(frame, 0, _lastFrame, 0, frame.Length);
		}

		public IObservable<byte[]> GetGray2Frames()
		{
			return _framesGray2;
		}
	}
}
