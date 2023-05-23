using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	public class ZeDMDComm // Class for locating the COM port of the ESP32 and communicating with it
	{
		public string nCOM;
		public const int BaudRate = 921600;
		private const int SERIAL_TIMEOUT = 120;
		public bool Opened = false;
		private SerialPort _serialPort;
		private const int MAX_SERIAL_WRITE_AT_ONCE = 8192;
		private const int SLOW_FRAMES_THRESHOLD = 4096;
		private const int FRAME_BUFFER_SIZE = 128;
		private const int COMMAND_SIZE_LIMIT = 100; // any buffer shorter then 100 is considered to be command like "reset palette"
		public const int N_CTRL_CHARS = 6;
		public const int N_INTERMEDIATE_CTR_CHARS = 4;
		public static readonly byte[] CtrlCharacters = { 0x5a, 0x65, 0x64, 0x72, 0x75, 0x6d };

		private BlockingCollection<byte[]> _frames = new BlockingCollection<byte[]>(FRAME_BUFFER_SIZE);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ZeDMDComm()
		{
			Task.Run(() =>
			{
				Logger.Info("Starting ZeDMD frame thread.");
				byte[] frame = null;
				bool sleep = false;
				int bufferedFramesThreshold = FRAME_BUFFER_SIZE;
				while (true) {
					try {
						frame = _frames.Take();
						if (frame.Length < COMMAND_SIZE_LIMIT) {
							// we might have a new mode, try to avoid sleeps until an error occurs
							sleep = false;
							bufferedFramesThreshold = FRAME_BUFFER_SIZE;
						}
						else if (frame.Length > SLOW_FRAMES_THRESHOLD) {
							sleep = false;
							bufferedFramesThreshold = 4;
						}
						else {
							bufferedFramesThreshold = 32;
						}

						// in case of an error, activate sleeps to slow down
						sleep = !StreamBytes(frame) || sleep;

						// for some modes it is important to let ZeDMD perform its rendering, before sending the next frame
						if (sleep) {
							System.Threading.Thread.Sleep(8);
						}
					}
					catch (InvalidOperationException) { }

					// in case ZeDMD falls behind, drop some frames
					while (_frames.Count >= bufferedFramesThreshold) {
						// drop frame
						frame = _frames.Take();
						if (frame.Length < COMMAND_SIZE_LIMIT) {
							// in case of a command, drop all frames and re-add the command
							while (_frames.Count > 0) {
								_frames.Take();
							}
							_frames.Add(frame);
						}
					}
				}
			});
		}

		public void QueueFrame(byte[] frame)
		{
			Task.Run(() =>
			{
				if (_frames.Count < FRAME_BUFFER_SIZE) {
					byte[] buffer;

					if (frame.Length > 1) {
						byte[] pCompressedBytes;
						NetMiniZ.NetMiniZ.MZCompress(frame.Skip(1).ToArray(), out pCompressedBytes);

						buffer = new byte[CtrlCharacters.Length + 1 + 2 + pCompressedBytes.Length];
						CtrlCharacters.CopyTo(buffer, 0);
						buffer[CtrlCharacters.Length] = frame[0];
						buffer[CtrlCharacters.Length + 1] = (byte)((pCompressedBytes.Length >> 8) & 0xFF);
						buffer[CtrlCharacters.Length + 2] = (byte)(pCompressedBytes.Length & 0xFF);
						pCompressedBytes.CopyTo(buffer, CtrlCharacters.Length + 3);
					} else {
						buffer = new byte[CtrlCharacters.Length + 1];
						CtrlCharacters.CopyTo(buffer, 0);
						frame.CopyTo(buffer, CtrlCharacters.Length);
					}

					_frames.Add(buffer);
				}
			});
		}

		private void SafeClose()
		{
			try
			{
				// In case of error discard serial data and close
				//_serialPort.DiscardInBuffer();
				//_serialPort.DiscardOutBuffer();
				_serialPort.Close();
				System.Threading.Thread.Sleep(100); // otherwise the next device will fail
			}
			catch { };
		}

		private bool Connect(string port, out int width, out int height)
		{
			// Try to find an ESP32 on the COM port and check if it answers with the shake-hand bytes
			try
			{
				_serialPort = new SerialPort(port, BaudRate, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One)
				{
					ReadTimeout = SERIAL_TIMEOUT,
					WriteBufferSize = MAX_SERIAL_WRITE_AT_ONCE,
					WriteTimeout = SERIAL_TIMEOUT
				};
				_serialPort.Open();
				_serialPort.Write(CtrlCharacters.Concat(new byte[] { 12 }).ToArray(), 0, CtrlCharacters.Length + 1);
				System.Threading.Thread.Sleep(200);
				var result = new byte[Math.Max(N_CTRL_CHARS + 1, N_INTERMEDIATE_CTR_CHARS + 4)];
				_serialPort.Read(result, 0, N_INTERMEDIATE_CTR_CHARS + 4);
				System.Threading.Thread.Sleep(200);
				if (!result.Take(4).SequenceEqual(CtrlCharacters.Take(4)))
				{
					SafeClose();
					width = 0;
					height = 0;
					return false;
				}
				width = result[N_INTERMEDIATE_CTR_CHARS] + result[N_INTERMEDIATE_CTR_CHARS + 1] * 256;
				height = result[N_INTERMEDIATE_CTR_CHARS + 2] + result[N_INTERMEDIATE_CTR_CHARS + 3] * 256;

				if (_serialPort.ReadByte() == 'R')
				{
					// enable compression
					_serialPort.Write(CtrlCharacters.Concat(new byte[] { 14 }).ToArray(), 0, CtrlCharacters.Length + 1);
					System.Threading.Thread.Sleep(4);

					if (_serialPort.ReadByte() == 'A' && _serialPort.ReadByte() == 'R')
					{
						// increase serial transfer chunk size

						_serialPort.Write(CtrlCharacters.Concat(new byte[] { 13, MAX_SERIAL_WRITE_AT_ONCE / 256 }).ToArray(), 0, CtrlCharacters.Length + 2);
						System.Threading.Thread.Sleep(4);

						if (_serialPort.ReadByte() == 'A')
						{
							nCOM = port;
							Opened = true;
							return true;
						}
					}
				}
			}
			catch
			{
				if (_serialPort != null && _serialPort.IsOpen) SafeClose();
			}
			width = 0;
			height = 0;
			return false;
		}

		private void Disconnect()
		{
			Opened = false;
			if (_serialPort != null) SafeClose();
		}

		private bool StreamBytes(byte[] pBytes)
		{
			if (_serialPort.IsOpen)
			{
				try
				{
					if (_serialPort.ReadByte() == 'R')
					{
						// send bytes
						int position = 0;
						while (position < pBytes.Length) {
							_serialPort.Write(pBytes, position, ((pBytes.Length - position) < MAX_SERIAL_WRITE_AT_ONCE) ? (pBytes.Length - position) : MAX_SERIAL_WRITE_AT_ONCE);
							if (_serialPort.ReadByte() == 'A') {
								// Received (A)cknowledge, ready to send the next chunk.
								position += MAX_SERIAL_WRITE_AT_ONCE;
							} else {
								// Something went wrong. Terminate current transmission of the buffer and return.
								return false;
							}
						}

						return true;
					}
				}
				catch (Exception e)
				{
					Logger.ForExceptionEvent(e).Log();
				}
			}

			return false;
		}

		public int Open(out int width, out int height)
		{
			// Try to find an ZeDMD on each COM port available
			bool IsAvailable = false;
			var ports = SerialPort.GetPortNames();
			width = 0;
			height = 0;
			foreach (var portName in ports)
			{
				IsAvailable = Connect(portName, out width, out height);
				if (IsAvailable) break;
			}
			if (!IsAvailable) return 0;

			Opened = true;
			return 1;
		}

		public bool Close()
		{
			if (Opened)
			{
				Disconnect();
			}

			Opened = false;
			return true;
		}

		protected static byte[] Compress(byte[] inData)
		{
			using (var outStream = new MemoryStream())
			using (var inStream = new MemoryStream(inData)) {
				NetMiniZ.NetMiniZ.Compress(inStream, outStream, 9);
				return outStream.ToArray();
			}
		}
	}
}

