﻿using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class SourceColoredGray6 : IColoredGray6Source, ITestSource<ColoredFrame>
	{
		public string Name => "Source[Gray6]";

		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private readonly Subject<ColoredFrame> _frames = new Subject<ColoredFrame>();

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _frames;

		public void AddFrame(ColoredFrame frame)
		{
			_frames.OnNext(frame);
		}
	}
}
