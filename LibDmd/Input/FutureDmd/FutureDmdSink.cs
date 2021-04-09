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
				var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message);

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
						if (chunkSize > 0)
						{
							 // The Logger call, on occasion, interupts the flow of frames. 
							 // This causes rendering to appear stuttering, as frames seem to be missed. 
							 // Perhaps it should not be called from this thread alternatively used only in debug mode or with an option to enable it.
							 // *** Leaving it in for now ***
							Logger.Info($"Got chunk ({chunkSize}):\n" + Encoding.UTF8.GetString(messageChunk.Take(chunkSize).ToArray()));
						}

						// game table has ended, clear the DMD - chunkSize is only 4 if "done" is recieved from the pipe
						if (chunkSize == 4) messageChunk = GetEmptyMessage(messageChunk.Length);

					} while (chunkSize != 0 || !server.IsMessageComplete);

					// convert message to frame data
					var frame = ConvertMessage(messageChunk);

					// publish frame data
					_framesGray4.OnNext(gray2Frame.Update(frame));

					// disconnect as the pipe was consumed
					server.Disconnect();

					// don't want this thread to consume 99% of this treads CPU, thus need to yield for some 5 ms, hence the need of a thread sleep call.
					Thread.Sleep(5);

					// TODO: 5 ms sleep seems a bit arbitrary. 
				    // It needs to be low enough not to skip an incoming frame, but still high enough not to hog down a CPU core. 
				    // After tests on some DMD intense tables, the conclusion is that it needs to be set really low (10 or less) for some tables, or frames might be skipped. 
                    // Tweaking the "wait" for optimized performance as an option maybe? 

				} while (isGameRunning);

				Logger.Info($"Pipe server for FutureDMD terminated!");

			}
			catch (IOException e)
			{
				Logger.Error(e);
			}
		}

		#region "FutureDMD helper methods"

		/// <summary>
		/// Converts a frame message byte array to format that Gray2Frame accepts.
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		private static byte[] ConvertMessage(byte[] message)
		{
			byte[] arr = new byte[message.Length];
			Parallel.For(0, message.Length,
							 index =>
							 {
								 arr[index] = GetByteFromHexCharValue((char)message[index]);
							 });

			return arr;
		}

		/// <summary>
		/// Returns an empty byte array used to clear the DMD
		/// </summary>
		/// <param name="length"></param>
		public static byte[] GetEmptyMessage(int length)
		{
			byte[] arr = new byte[length];
			Parallel.For(0, length,
				 index =>
				 {
					 arr[index] = 0x00;
				 });

			return arr;
		}

		private const string _hexValues = "0123456789ABCDEF";
		/// <summary>
		/// Convert a numerical char representation to a byte value 
		/// </summary>
		/// <param name="c"></param>
		private static byte GetByteFromHexCharValue(char c)
		{
			if (c > 0 && _hexValues.Contains(c)) return (byte)Convert.ToInt32(c.ToString(), 16);
			return 0;
		}

		#endregion
	}
}
