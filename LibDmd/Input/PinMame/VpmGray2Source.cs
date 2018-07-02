﻿using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using NLog.Targets;

namespace LibDmd.Input.PinMame
{
	/// <summary>
	/// Receives 2-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class VpmGray2Source : AbstractSource, IGray2Source
	{
		public override string Name { get; } = "VPM 2-bit Source";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<byte[]> _framesGray2 = new Subject<byte[]>();
		
		public void NextFrame(int width, int height, byte[] frame)
		{
			SetDimensions(width, height);
			_framesGray2.OnNext(frame);
		}

		public IObservable<byte[]> GetGray2Frames()
		{
			return _framesGray2;
		}
	}
}
