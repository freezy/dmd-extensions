using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace LibDmd.Input.Network
{
	public class WebsocketGray2Source : AbstractSource, IGray2Source
	{
		public override string Name => "Websocket 2-bit Source";

		IObservable<Unit> ISource.OnResume => _onResume;
		IObservable<Unit> ISource.OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<byte[]> _framesGray2 = new Subject<byte[]>();

		public IObservable<byte[]> GetGray2Frames()
		{
			return _framesGray2;
		}
	}
}
