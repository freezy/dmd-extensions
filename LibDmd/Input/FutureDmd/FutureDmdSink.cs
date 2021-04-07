using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace LibDmd.Input.FutureDmd
{
	/// <summary>
	/// Opens a pipe server for FutureDMD to connect to.
	/// </summary>
	public class FutureDmdSink : AbstractSource, IGray4Source
	{
		public override string Name { get; } = "Future DMD";

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private const string PipeName = "futuredmd"; 
		private Thread _thread;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DMDFrame> _framesGray4 = new Subject<DMDFrame>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public FutureDmdSink() {
			// spawn new thread
			Logger.Info($"Starting pipe server for FutureDMD..");
			_thread = new Thread(ServerThread);
			_thread.Start();

			// that's from the ms doc!
			Thread.Sleep(250);
		}


		public IObservable<DMDFrame> GetGray4Frames()
		{
			return _framesGray4;
		}

		private void ServerThread(object data) 
		{
			var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.None);

			// wait for future pinball to connect
			server.WaitForConnection();

			Logger.Info($"FutureDMD connected to pipe.");


			Logger.Info($"Pipe server for FutureDMD terminated!");
		}
	}
}
