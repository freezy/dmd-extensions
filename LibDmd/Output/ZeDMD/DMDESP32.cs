using System;
using System.IO.Ports;

namespace DMDESP32
{
	public class DMD_ESP32 // Class for locating the COM port of the ESP32 and communicating with it
	{
		public string nCOM;
		public int BaudRate;
		public byte ByteSize = 8;
		public bool Opened = false;
		public byte Parity = 0; // 0-4=no,odd,even,mark,space
		public string Port;
		public int ReadTimeout;
		public byte StopBits = 1; // 0,1,2 = 1, 1.5, 2*/
		private SerialPort _serialPort;
		private static readonly int _MAX_SERIAL_WRITE_AT_ONCE = 9400;
		public static readonly int N_CTRL_CHARS = 6;
		public static readonly int N_INTERMEDIATE_CTR_CHARS = 4;
		public static readonly byte[] CtrlCharacters = { (byte)0x5a, (byte)0x65, (byte)0x64, (byte)0x72, (byte)0x75, (byte)0x6d };
		private bool _Scom_Connect(string port, ref int rx, ref int ry)
		{
			try
			{
				BaudRate = 921600;
				Port = port;
				_serialPort = new SerialPort(port, BaudRate, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
				_serialPort.ReadTimeout = 100;
				_serialPort.WriteBufferSize = _MAX_SERIAL_WRITE_AT_ONCE + 100;
				_serialPort.WriteTimeout = SerialPort.InfiniteTimeout;
				_serialPort.Open();
				var result = new byte[Math.Max(N_CTRL_CHARS + 1, N_INTERMEDIATE_CTR_CHARS + 4)];
				for (int i = 0; i < N_CTRL_CHARS; i++) result[i] = CtrlCharacters[i];
				result[N_CTRL_CHARS] = 12; // ask for resolution
				_serialPort.Write(result, 0, N_CTRL_CHARS + 1);
				System.Threading.Thread.Sleep(100);
				_serialPort.Read(result, 0, N_INTERMEDIATE_CTR_CHARS + 4);
				System.Threading.Thread.Sleep(200);
				for (int i = 0; i < N_INTERMEDIATE_CTR_CHARS; i++)
				{
					if (result[i] != CtrlCharacters[i])
					{
						_serialPort.DiscardInBuffer();
						_serialPort.DiscardOutBuffer();
						_serialPort.Close();
						return false;
					}
				}
				rx = (int)result[N_INTERMEDIATE_CTR_CHARS] + (int)result[N_INTERMEDIATE_CTR_CHARS + 1] * 256;
				ry = (int)result[N_INTERMEDIATE_CTR_CHARS + 2] + (int)result[N_INTERMEDIATE_CTR_CHARS + 3] * 256;
				nCOM = port;
				return true;

			}
			catch
			{
				if (_serialPort != null && _serialPort.IsOpen)
				{
					_serialPort.DiscardInBuffer();
					_serialPort.DiscardOutBuffer();
					_serialPort.Close();
					System.Threading.Thread.Sleep(100); // otherwise the next device will fail
				}

			}
			return false;
		}
		private void Scom_Disconnect()
		{
			if (_serialPort != null)// && _serialPort.IsOpen)
			{
				_serialPort.DiscardInBuffer();
				_serialPort.DiscardOutBuffer();
				_serialPort.Close();
				System.Threading.Thread.Sleep(100); // otherwise the next device will fail
			}
		}
		public bool Scom_SendBytes(byte[] pBytes, int nBytes)
		{
			if (_serialPort.IsOpen)
			{
				try
				{
					_serialPort.Write(pBytes, 0, nBytes);
					System.Threading.Thread.Sleep(40);// 2000 / (BaudRate / (nBytes * 8)));
													  //_serialPort.DiscardOutBuffer();
					return true;
				}
				catch
				{
					_serialPort.DiscardInBuffer();
					_serialPort.DiscardOutBuffer();
				};
			}
			return false;
		}
		public bool Scom_SendBytes3(byte[] pBytes, int nBytes)
		{
			if (_serialPort.IsOpen)
			{
				byte[] pBytes2 = new byte[12 + _MAX_SERIAL_WRITE_AT_ONCE]; // 4 pour la synchro + 4 pour la taille du transfert
				int remainTrans = nBytes - 8; // la totalité - les bytes de synchro
				try
				{
					// premier transfert
					for (uint i = 0; i < 8; i++) pBytes2[i] = pBytes[i];
					int qtetrans = Math.Min(remainTrans, _MAX_SERIAL_WRITE_AT_ONCE);
					pBytes2[8] = (byte)(qtetrans & 0xff);
					pBytes2[9] = (byte)((qtetrans >> 8) & 0xff);
					pBytes2[10] = (byte)((qtetrans >> 16) & 0xff);
					pBytes2[11] = (byte)((qtetrans >> 24) & 0xff);
					Buffer.BlockCopy(pBytes, 8, pBytes2, 12, qtetrans);
					_serialPort.Write(pBytes2, 0, qtetrans + 12);
					remainTrans -= qtetrans;
					System.Threading.Thread.Sleep(1000 / (BaudRate / ((qtetrans + 12) * 8)));
					int ti = 1;
					while (remainTrans > 0)
					{
						qtetrans = Math.Min(remainTrans, _MAX_SERIAL_WRITE_AT_ONCE);
						pBytes2[0] = (byte)(qtetrans & 0xff);
						pBytes2[1] = (byte)((qtetrans >> 8) & 0xff);
						pBytes2[2] = (byte)((qtetrans >> 16) & 0xff);
						pBytes2[3] = (byte)((qtetrans >> 24) & 0xff);
						Buffer.BlockCopy(pBytes, 8 + ti * _MAX_SERIAL_WRITE_AT_ONCE, pBytes2, 4, qtetrans);
						_serialPort.Write(pBytes2, 0, qtetrans + 4);
						System.Threading.Thread.Sleep(1000 / (BaudRate / ((qtetrans + 4) * 8)));
						remainTrans -= qtetrans;
						ti++;
					}
					return true;
				}
				catch
				{
					_serialPort.DiscardInBuffer();
					_serialPort.DiscardOutBuffer();
					return false;
				};
			}
			return false;
		}
		public bool Scom_SendBytes2(byte[] pBytes, int nBytes)
		{
			if (_serialPort.IsOpen)
			{
				byte[] pBytes2 = new byte[N_CTRL_CHARS + _MAX_SERIAL_WRITE_AT_ONCE]; // 4 pour la synchro
				int remainTrans = nBytes; // la totalité - les bytes de synchro
				try
				{
					// premier transfert
					for (int i = 0; i < N_CTRL_CHARS; i++) pBytes2[i] = CtrlCharacters[i];
					int qtetransf = Math.Min(_MAX_SERIAL_WRITE_AT_ONCE - N_CTRL_CHARS, remainTrans);
					Buffer.BlockCopy(pBytes, 0, pBytes2, N_CTRL_CHARS, qtetransf);
					_serialPort.Write(pBytes2, 0, qtetransf + N_CTRL_CHARS);
					System.Threading.Thread.Sleep(Math.Min(20, 1000 / (BaudRate / ((qtetransf + N_CTRL_CHARS) * 8))));
					remainTrans -= qtetransf;
					int ti = qtetransf;
					while (remainTrans > 0)
					{
						for (int i = N_INTERMEDIATE_CTR_CHARS - 1; i >= 0; i--) pBytes2[N_INTERMEDIATE_CTR_CHARS - 1 - i] = CtrlCharacters[i];
						qtetransf = Math.Min(remainTrans, _MAX_SERIAL_WRITE_AT_ONCE - N_INTERMEDIATE_CTR_CHARS);
						Buffer.BlockCopy(pBytes, ti, pBytes2, N_INTERMEDIATE_CTR_CHARS, qtetransf);
						_serialPort.Write(pBytes2, 0, qtetransf + N_INTERMEDIATE_CTR_CHARS);
						System.Threading.Thread.Sleep(Math.Min(20, 1000 / (BaudRate / ((qtetransf + N_INTERMEDIATE_CTR_CHARS) * 8))));
						remainTrans -= qtetransf;
						ti += qtetransf;
					}
					return true;
				}
				catch
				{
					_serialPort.DiscardInBuffer();
					_serialPort.DiscardOutBuffer();
					return false;
				};
			}
			return false;
		}
		public void ResetPalettes()
		{
			byte[] tempbuffer = new byte[4];
			tempbuffer[0] = 0x81; // frame sync bytes
			tempbuffer[1] = 0xC3;
			tempbuffer[2] = 0xE7;
			tempbuffer[3] = 0x6;  // command byte 6 = reset palettes
			Scom_SendBytes(tempbuffer, 4);
			System.Threading.Thread.Sleep(20);
		}
		public int Scom_Open(ref int rx, ref int ry)
		{
			bool IsAvailable = false;
			var ports = SerialPort.GetPortNames();
			foreach (var portName in ports)
			{
				IsAvailable = _Scom_Connect(portName, ref rx, ref ry);
				if (IsAvailable) break;
			}
			if (!IsAvailable) return 0;
			ResetPalettes();
			Opened = true;
			return 1;
		}

		public bool Scom_Close()
		{
			if (Opened)
			{
				Scom_Disconnect();
				ResetPalettes();
			}

			Opened = false;
			return true;
		}

	}
}

