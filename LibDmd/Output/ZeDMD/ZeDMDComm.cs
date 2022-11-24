﻿using System;
using System.IO.Ports;
using System.Linq;

namespace LibDmd.Output.ZeDMD
{
	public class ZeDMDComm // Class for locating the COM port of the ESP32 and communicating with it
	{
		public string nCOM;
		public const int BaudRate = 921600;
		public bool Opened = false;
		private SerialPort _serialPort;
		private const int MAX_SERIAL_WRITE_AT_ONCE = 9400;
		public const int N_CTRL_CHARS = 6;
		public const int N_INTERMEDIATE_CTR_CHARS = 4;
		public static readonly byte[] CtrlCharacters = { 0x5a, 0x65, 0x64, 0x72, 0x75, 0x6d };
		byte[] pBytes2 = new byte[N_CTRL_CHARS + MAX_SERIAL_WRITE_AT_ONCE]; // 4 pour la synchro

		private void SafeClose()
		{
			// In case of error discard serial data and close
			_serialPort.DiscardInBuffer();
			_serialPort.DiscardOutBuffer();
			_serialPort.Close();
			System.Threading.Thread.Sleep(100); // otherwise the next device will fail
		}
		private bool Connect(string port, out int width, out int height)
		{
			// Try to find an ESP32 on the COM port and check if it answers with the shake-hand bytes
			try
			{
				_serialPort = new SerialPort(port, BaudRate, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One)
				{
					ReadTimeout = 100,
					WriteBufferSize = MAX_SERIAL_WRITE_AT_ONCE + 100,
					WriteTimeout = SerialPort.InfiniteTimeout
				};
				_serialPort.Open();
				_serialPort.Write(CtrlCharacters.Concat(new byte[] { 12 }).ToArray(), 0, CtrlCharacters.Length + 1);
				System.Threading.Thread.Sleep(100);
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
			if (_serialPort != null) SafeClose();
		}
		public bool SendBytes(byte[] pBytes, int nBytes)
		{
			// Send a buffer of Data in one transfer
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
/*		public bool StreamBytes2(byte[] pBytes, int nBytes)
		{
			// Send a big buffer in several transfer sending each length
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
		}*/
		public bool StreamBytes(byte[] pBytes, int nBytes)
		{
			// Send a big buffer in several transfers to avoid corrupted data
			if (_serialPort.IsOpen)
			{
				int remainTrans = nBytes; // la totalité - les bytes de synchro
				try
				{
					// premier transfert
					for (int i = 0; i < N_CTRL_CHARS; i++) pBytes2[i] = CtrlCharacters[i];
					int qtetransf = Math.Min(MAX_SERIAL_WRITE_AT_ONCE - N_CTRL_CHARS, remainTrans);
					Buffer.BlockCopy(pBytes, 0, pBytes2, N_CTRL_CHARS, qtetransf);
					_serialPort.Write(pBytes2, 0, qtetransf + N_CTRL_CHARS);
					System.Threading.Thread.Sleep(Math.Min(20, 1000 / (BaudRate / ((qtetransf + N_CTRL_CHARS) * 8))));
					remainTrans -= qtetransf;
					int ti = qtetransf;
					while (remainTrans > 0)
					{
						for (int i = N_INTERMEDIATE_CTR_CHARS - 1; i >= 0; i--) pBytes2[N_INTERMEDIATE_CTR_CHARS - 1 - i] = CtrlCharacters[i];
						qtetransf = Math.Min(remainTrans, MAX_SERIAL_WRITE_AT_ONCE - N_INTERMEDIATE_CTR_CHARS);
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
			// Reset ESP32 palette
			byte[] tempbuffer = new byte[4];
			tempbuffer[0] = 0x81; // frame sync bytes
			tempbuffer[1] = 0xC3;
			tempbuffer[2] = 0xE7;
			tempbuffer[3] = 0x6;  // command byte 6 = reset palettes
			SendBytes(tempbuffer, 4);
			System.Threading.Thread.Sleep(20);
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

