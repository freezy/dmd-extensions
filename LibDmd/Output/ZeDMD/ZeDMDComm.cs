using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;

namespace LibDmd.Output.ZeDMD
{
	public class ZeDMDComm // Class for locating the COM port of the ESP32 and communicating with it
	{
		public string nCOM;
		public const int BaudRate = 921600;
		public bool Opened = false;
		private SerialPort _serialPort;
		private const int MAX_SERIAL_WRITE_AT_ONCE = 256;
		public const int N_CTRL_CHARS = 6;
		public const int N_INTERMEDIATE_CTR_CHARS = 4;
		public static readonly byte[] CtrlCharacters = { 0x5a, 0x65, 0x64, 0x72, 0x75, 0x6d };
		private string _portName;
		private byte[] lastc4 = new byte[4];
		private int[] lastlc4 = new int[4];
		private byte aclc4 = 0;
		private byte[] _frameBufferSaved = new byte[1 + 256 * 64 * 4];




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
					SafeClose();
					width = 0;
					height = 0;
					return false;
				}
				width = result[N_INTERMEDIATE_CTR_CHARS] + result[N_INTERMEDIATE_CTR_CHARS + 1] * 256;
				height = result[N_INTERMEDIATE_CTR_CHARS + 2] + result[N_INTERMEDIATE_CTR_CHARS + 3] * 256;
				nCOM = port;
				Opened = true;
				return true;

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
		public bool StreamBytes(byte[] pBytes, int nBytes)
		{
			int step = 0;
			if (_serialPort.IsOpen)
			{
				try
				{
					if (_serialPort.ReadByte() == 'R')
					{
						step++;
						// first send
						byte[] pBytes2 = new byte[MAX_SERIAL_WRITE_AT_ONCE];
						for (int ti = 0; ti < N_CTRL_CHARS; ti++) pBytes2[ti] = CtrlCharacters[ti];
						step++;
						int tosend = (nBytes < MAX_SERIAL_WRITE_AT_ONCE - N_CTRL_CHARS) ? nBytes : (MAX_SERIAL_WRITE_AT_ONCE - N_CTRL_CHARS);
						for (int ti = 0; ti < tosend; ti++) pBytes2[ti + N_CTRL_CHARS] = pBytes[ti];
						step++;
						_serialPort.Write(pBytes2, 0, tosend + N_CTRL_CHARS);
						step++;
						if (_serialPort.ReadByte() != 'A') return false;
						// next ones
						step++;
						int bufferPosition = tosend;
						step++;
						while (bufferPosition < nBytes)
						{
							tosend = (nBytes - bufferPosition < MAX_SERIAL_WRITE_AT_ONCE) ? nBytes - bufferPosition : MAX_SERIAL_WRITE_AT_ONCE;
							_serialPort.Write(pBytes, bufferPosition, tosend);
							if (_serialPort.ReadByte() == 'A')
							{
								// Received (A)cknowledge, ready to send the next chunk.
								bufferPosition += tosend;
								step++;
							}
							else
							{
								// Something went wrong. Terminate current transmission of the buffer and return.
								return false;
							}
						}






						lastc4[aclc4] = pBytes[0];
						lastlc4[aclc4] = nBytes;
						aclc4++;
						if (aclc4 == 4) aclc4 = 0;
						for (int ti = 0; ti < nBytes; ti++) _frameBufferSaved[ti] = pBytes[ti];






						return true;
					}
					else
					{
						uint trh = 12;
					}
				}
				catch (Exception e)
				{
					return false;
				}
			}
			int tcl = aclc4 - 1;
			if (tcl < 0) tcl = 3;
			using (BinaryWriter binWriter = new BinaryWriter(File.Open("d:\\dump.bin", FileMode.Create)))
			{
				for (int ti=0;ti< lastlc4[tcl];ti++) binWriter.Write(_frameBufferSaved[ti]);
			}
			return false;
		}
		public void ResetPalettes()
		{
			// Reset ESP32 palette
			byte[] tempbuf = new byte[1];
			tempbuf[0] = 0x6;  // command byte 6 = reset palettes
			StreamBytes(tempbuf, 1);
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

	}
}

