using System;
using System.Collections.Generic;
using System.IO;
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
		private readonly Thread _thread;

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
			try
			{
				var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message);
				server.WaitForConnection();

				var isGameRunning = true;
				var chunkSize = 0;
				var gray2Frame = new DMDFrame { width = 132, height = 32 };
				var messageChunk = new byte[4096];
				
				// for each frame
				do
				{
					// for each chunk
					do
					{
						chunkSize = server.Read(messageChunk, 0, messageChunk.Length);
						if (chunkSize > 0) {
							Logger.Info($"Got chunk ({chunkSize}):\n" + Encoding.UTF8.GetString(messageChunk.Take(chunkSize).ToArray()));
						}

					} while (!server.IsMessageComplete);

					// TODO convert message to frame data
					var frame = messageChunk;

					// publish frame data
					_framesGray4.OnNext(gray2Frame.Update(frame));


				} while (isGameRunning);

				server.Disconnect();
				Logger.Info($"Pipe server for FutureDMD terminated!");

			} catch (IOException e) {
				Logger.Error(e);
			}
		}
	}
}
