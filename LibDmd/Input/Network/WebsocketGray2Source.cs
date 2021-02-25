using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.Network
{
	public class WebsocketGray2Source : AbstractSource, IGray2Source
	{
		public override string Name => "Websocket 2-bit Source";

		IObservable<Unit> ISource.OnResume => _onResume;
		IObservable<Unit> ISource.OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		public readonly Subject<DMDFrame> FramesGray2 = new Subject<DMDFrame>();

		public IObservable<DMDFrame> GetGray2Frames()
		{
			return FramesGray2;
		}
	}
}
