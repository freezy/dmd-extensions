using System;
using System.IO;
using System.IO.Pipes;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
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
    	private const int NumThreads = 2;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		private readonly Subject<DMDFrame> _framesGray4 = new Subject<DMDFrame>();
		private byte[] _frame;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public FutureDmdSink()
		{
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
				var server = new NamedPipeServerStream(PipeName, PipeDirection.In, NumThreads, PipeTransmissionMode.Message);

				var isGameRunning = true; 
				var chunkSize = 0;
				var gray2Frame = new DMDFrame { width = 132, height = 32 };
				var messageChunk = new byte[4096];

				// for each frame
				do
				{
					// connect and wait for a new frame
					server.WaitForConnection();

					// for each chunk
					do
					{
						chunkSize = server.Read(messageChunk, 0, messageChunk.Length);

						// game table has ended, clear the DMD with an empty byte array - chunkSize is only 4 if "done" is recieved from the pipe
						if (chunkSize == 4) {
							messageChunk = new byte[messageChunk.Length];
						}

					} while (chunkSize != 0 || !server.IsMessageComplete);

					// convert message to frame data
					UpdateFrame(messageChunk);

					// publish frame data
					_framesGray4.OnNext(gray2Frame.Update(_frame));

					// disconnect as the pipe was consumed
					server.Disconnect();

					// don't want this thread to consume 99% of this treads CPU, thus need to yield for some 5-10 ms, hence the need of a thread sleep call.
					Thread.Sleep(10);
					// 10 ms sleep might seem a bit arbitrary. 
					// It needs to be low enough not to skip an incoming frame, but still high enough not to hog down a CPU core. 
					// After tests on some DMD intense tables, the conclusion is that it needs to be set really low (10 or less) for some tables, or frames might be skipped. 

				} while (isGameRunning);

				Logger.Info($"Pipe server for FutureDMD terminated!");

				if (server != null) {
					server.Dispose();
				}
				server = null;
			
			} catch (IOException e) {
				Logger.Error(e);
			}
		}

		/// <summary>
		/// Converts a frame message byte array to format that Gray2Frame accepts.
		/// </summary>
		/// <param name="message"></param>
		private void UpdateFrame(byte[] message)
		{
			// this needs method needs to return a new byte array, otherwise the DMD will "flicker"
			if (_frame == null || _frame.Length != message.Length) {
				_frame = new byte[message.Length];
			}

			for (int i = 0; i < message.Length; i++) {
				_frame[i] = GetShaderValueFromHexByte(message[i]);
			}
		}

		/// <summary>
		/// Converts one byte (hex char) numerical representation to a byte shader value
		/// </summary>
		/// <param name="c"></param>
		private static byte GetShaderValueFromHexByte(byte c)
		{
			return c < 58
				? (byte)Math.Max(c - 48, 0)
				: (byte)Math.Min(c - 55, 15);
		}
	}
}
