﻿using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// Receives 4-bit frames from VPM and forwards them to the observable
	/// after dropping duplicates.
	/// </summary>
	public class PassthroughGray4Source : AbstractSource, IGray4Source, IGameNameSource
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DMDFrame> _framesGray4 = new Subject<DMDFrame>();
		private readonly ISubject<string> _gameName = new Subject<string>();
		private readonly BehaviorSubject<FrameFormat> _lastFrameFormat;

		public PassthroughGray4Source(BehaviorSubject<FrameFormat> lastFrameFormat, string name)
		{
			_lastFrameFormat = lastFrameFormat;
			Name = name;
		}

		public void NextFrame(DMDFrame frame)
		{
			SetDimensions(frame.width, frame.height);
			_framesGray4.OnNext(frame);
			_lastFrameFormat.OnNext(FrameFormat.Gray4);
		}

		public IObservable<DMDFrame> GetGray4Frames() => _framesGray4;

		public void NextGameName(string gameName) => _gameName.OnNext(gameName);

		public IObservable<string> GetGameName() => _gameName;
	}
}
