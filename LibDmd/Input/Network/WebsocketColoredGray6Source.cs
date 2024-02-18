using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.Network
{
	public class WebsocketColoredGray6Source : AbstractSource, IColoredGray6Source
	{
		public override string Name => "Websocket Colored 6-bit Source";

		IObservable<Unit> ISource.OnResume => _onResume;
		IObservable<Unit> ISource.OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		public readonly Subject<ColoredFrame> FramesColoredGray6 = new Subject<ColoredFrame>();

		public IObservable<ColoredFrame> GetColoredGray6Frames()
		{
			return FramesColoredGray6;
		}
	}
}
