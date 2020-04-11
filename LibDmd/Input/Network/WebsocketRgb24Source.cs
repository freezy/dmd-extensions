﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Input.Network
{
	public class WebsocketRgb24Source : AbstractSource, IRgb24Source
	{
		public override string Name => "Websocket 24-bit RGB Source";

		IObservable<Unit> ISource.OnResume => _onResume;
		IObservable<Unit> ISource.OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		public readonly Subject<byte[]> FramesRgb24 = new Subject<byte[]>();

		public IObservable<byte[]> GetRgb24Frames()
		{
			return FramesRgb24;
		}
	}
}
