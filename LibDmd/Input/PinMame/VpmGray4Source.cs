﻿using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Common;

namespace LibDmd.Input.PinMame
{
	/// <summary>
	/// Receives 4-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class VpmGray4Source : AbstractSource, IGray4Source
	{
		public override string Name { get; } = "VPM 4-bit Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<byte[]> _framesGray4 = new Subject<byte[]>();
		private byte[] _lastFrame;

		public void NextFrame(int width, int height, byte[] frame)
		{
			if (_lastFrame?.Length != frame.Length) {
				_lastFrame = new byte[frame.Length];
			}
			SetDimensions(width, height);
			_framesGray4.OnNext(frame);
			Buffer.BlockCopy(frame, 0, _lastFrame, 0, frame.Length);
		}

		public IObservable<byte[]> GetGray4Frames()
		{
			return _framesGray4;
		}
	}
}
