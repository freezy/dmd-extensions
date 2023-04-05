using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Markup;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	public class ZeDMDComm // Class for locating the COM port of the ESP32 and communicating with it
	{
		public string nCOM;
		public const int BaudRate = 921600;
		public bool Opened = false;
		private SerialPort _serialPort;
		private const int MAX_SERIAL_WRITE_AT_ONCE = 8192;
		public const int N_CTRL_CHARS = 6;
		public const int N_INTERMEDIATE_CTR_CHARS = 4;
		public static readonly byte[] CtrlCharacters = { 0x5a, 0x65, 0x64, 0x72, 0x75, 0x6d };
		private string _portName;

		private BlockingCollection<byte[]> _frames = new BlockingCollection<byte[]>(128); // max queue size 128

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ZeDMDComm()
		{
			Task.Run(() => {
				Logger.Info("Starting ZeDMD frame thread.");
				while (!_frames.IsCompleted) {

					byte[] frame = null;
					try {
						frame = _frames.Take();

					} catch (InvalidOperationException) { }

					if (frame != null) {
						StreamBytes(frame);
					}
				}
				Logger.Info("ZeDMD frame thread finished.");
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
					ReadTimeout = 100,
					WriteBufferSize = MAX_SERIAL_WRITE_AT_ONCE,
					WriteTimeout = 100//SerialPort.InfiniteTimeout
				};
				_serialPort.Open();
				_serialPort.Write(CtrlCharacters.Concat(new byte[] { 12 }).ToArray(), 0, CtrlCharacters.Length + 1);
				System.Threading.Thread.Sleep(200);
				var result = new byte[Math.Max(N_CTRL_CHARS + 1, N_INTERMEDIATE_CTR_CHARS + 4)];
				_serialPort.Read(result, 0, N_INTERMEDIATE_CTR_CHARS + 4);
				System.Threading.Thread.Sleep(200);
				if (!result.Take(4).SequenceEqual(CtrlCharacters.Take(4)))
				{
					_frames.CompleteAdding();
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
					if (_serialPort.ReadByte() == 'A' && _serialPort.ReadByte() == 'R')
					{
						// increase serial transfer chunk size
						_serialPort.Write(CtrlCharacters.Concat(new byte[] { 13, MAX_SERIAL_WRITE_AT_ONCE / 256 }).ToArray(), 0, CtrlCharacters.Length + 1);
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
				_frames.CompleteAdding();
				if (_serialPort != null && _serialPort.IsOpen) SafeClose();
			}
			width = 0;
			height = 0;
			return false;
		}
		private void Disconnect()
		{
			Opened = false;
			_frames.CompleteAdding();
			if (_serialPort != null) SafeClose();
		}

		public void QueueFrame(byte[] frames)
		{
			Task.Run(() => _frames.Add(frames));
		}

		private bool StreamBytes(byte[] pBytes)
		{
			if (_serialPort.IsOpen)
			{
				try
				{
					if (_serialPort.ReadByte() == 'R')
					{
						// send control characters and command
						_serialPort.Write(CtrlCharacters, 0, CtrlCharacters.Length);
						_serialPort.Write(pBytes, 0, 1);

						// remove the command fr0m the data and compress the data
						byte[] pCompressedBytes = Compress(pBytes.Skip(1).ToArray());
						int nCompressedBytes = pCompressedBytes.Length;

						byte[] pCompressionHeader = new byte[2];
						pCompressionHeader[0] = (byte)((nCompressedBytes >> 8) & 0xFF);
						pCompressionHeader[1] = (byte)((nCompressedBytes & 0xFF));
						// send coompression header
						_serialPort.Write(pCompressionHeader, 0, 2);

						int chunk = MAX_SERIAL_WRITE_AT_ONCE - 6 - 1 - 2;
						int position = 0;
						while (position < nCompressedBytes) {
							_serialPort.Write(pCompressedBytes, position, ((nCompressedBytes - position) < chunk) ? (nCompressedBytes - position) : chunk);
							if (_serialPort.ReadByte() == 'A') {
								// Received (A)cknowledge, ready to send the next chunk.
								position += chunk;
								chunk = MAX_SERIAL_WRITE_AT_ONCE;
							}
							else
							{
								// Something went wrong. Terminate current transmission of the buffer and return.
								return false;
							}
						}
						return true;
					}
				}
				catch (Exception e)
				{
					return false;
				}
			}
			return false;
		}
		public void ResetPalettes()
		{
			// Reset ESP32 palette
			byte[] tempbuf = new byte[1];
			tempbuf[0] = 0x6;  // command byte 6 = reset palettes
			StreamBytes(tempbuf);
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
				_portName = portName;
				if (IsAvailable) break;
			}
			if (!IsAvailable) return 0;

			ResetPalettes();
			Opened = true;
			return 1;
		}

		public bool Close()
		{
			if (Opened)
			{
				Disconnect();
				ResetPalettes();
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

