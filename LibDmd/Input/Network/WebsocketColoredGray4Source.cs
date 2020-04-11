﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Input.Network
{
	public class WebsocketColoredGray4Source : AbstractSource, IColoredGray4Source
	{
		public override string Name => "Websocket Colored 4-bit Source";

		IObservable<Unit> ISource.OnResume => _onResume;
		IObservable<Unit> ISource.OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		public readonly Subject<ColoredFrame> FramesColoredGray4 = new Subject<ColoredFrame>();

		public IObservable<ColoredFrame> GetColoredGray4Frames()
		{
			return FramesColoredGray4;
		}
	}
}
