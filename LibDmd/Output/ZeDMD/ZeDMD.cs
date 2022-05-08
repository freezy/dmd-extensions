using System;
using System.Windows.Media;
using LibDmd.Common;
using NLog;
using System.IO.Ports;
using System.Diagnostics;

/// <summary>
/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32
/// Uses the library for ESP32 from mrfaptastic "ESP32-HUB75-MatrixPanel-I2S-DMA" (https://github.com/mrfaptastic/ESP32-HUB75-MatrixPanel-I2S-DMA)
/// so you need 2 64x32 pannels compatible (check the Readme.md in the link above), but for my tests I had 2 different pannels and it worked, the main constraint
/// is to have a 1/16 scan
/// you can check a video of a colorized rom here https://www.youtube.com/watch?v=IQQb7Jl1QW8
/// The aim for this new real DMD is to propose a really cheap device with open source full code (this code for Freezy dmd-extensions + the arduino IDE
/// C code to inject in the ESP32)
/// On Aliexpress, you can get a µC ESP32 for around 5$ (I suggest a 38-pin to be sure to have enough I/O, but check in the Arduino code for the number of pins needed),
/// 2 64x32-LED-matrix display for 15$-20$ each. So for less than 50$ you get a full real DMD!
/// I am thinking about designing a shield for a 38-pin ESP32, I will give the PCB layout once done (for free, sure), then you can 
/// </summary>

namespace DMDESP32
{
	public class DMD_ESP32 // Class for locating the COM port of the ESP32 and communicating with it
	{
		private int hCOM;
		public string nCOM;
		public int BaudRate;
		public byte ByteSize = 8;
		public bool Opened = false;
		public byte Parity = 0; // 0-4=no,odd,even,mark,space
		public string Port;
		public int ReadTimeout;
		public byte StopBits = 1; // 0,1,2 = 1, 1.5, 2*/
		private byte[] oldbuffer = new byte[16384];
		private SerialPort _serialPort;
		private static readonly uint GENERIC_READ = 0x80000000;
		private static readonly uint GENERIC_WRITE = 0x40000000;
		private static readonly int INVALID_HANDLE_VALUE = -1;
		private static readonly int OPEN_EXISTING = 3;
		private static readonly int MAX_SERIAL_WRITE_AT_ONCE = 9400;
		public static readonly int N_CTRL_CHARS = 6;
		public static readonly int N_INTERMEDIATE_CTR_CHARS = 4;
		public static readonly byte[] CtrlCharacters = { (byte)0x5a, (byte)0x65, (byte)0x64, (byte)0x72, (byte)0x75, (byte)0x6d };
		private bool Scom_Connect(string port,ref int rx,ref int ry)
		{
			try
			{
				BaudRate = 921600;
				Port = port;
				_serialPort = new SerialPort(port, BaudRate, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
				_serialPort.ReadTimeout = 100;
				_serialPort.WriteBufferSize = MAX_SERIAL_WRITE_AT_ONCE + 100;
				_serialPort.WriteTimeout = SerialPort.InfiniteTimeout;
				_serialPort.Open();
				var result = new byte[Math.Max(N_CTRL_CHARS + 1, N_INTERMEDIATE_CTR_CHARS + 4)];
				for (int i = 0; i < N_CTRL_CHARS; i++) result[i] = CtrlCharacters[i];
				result[N_CTRL_CHARS] = 12; // ask for resolution
				_serialPort.Write(result, 0, N_CTRL_CHARS + 1);
				System.Threading.Thread.Sleep(100);
				_serialPort.Read(result, 0, N_INTERMEDIATE_CTR_CHARS + 4);
				System.Threading.Thread.Sleep(200);
				for (int i=0;i< N_INTERMEDIATE_CTR_CHARS;i++)
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
		private void Scom_Disconnect(int comhandle)
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
				byte[] pBytes2 = new byte[12 + MAX_SERIAL_WRITE_AT_ONCE]; // 4 pour la synchro + 4 pour la taille du transfert
				int remainTrans = nBytes - 8; // la totalité - les bytes de synchro
				try
				{
					// premier transfert
					for (uint i = 0; i < 8; i++) pBytes2[i] = pBytes[i];
					int qtetrans = Math.Min(remainTrans, MAX_SERIAL_WRITE_AT_ONCE);
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
						qtetrans = Math.Min(remainTrans, MAX_SERIAL_WRITE_AT_ONCE);
						pBytes2[0] = (byte)(qtetrans & 0xff);
						pBytes2[1] = (byte)((qtetrans >> 8) & 0xff);
						pBytes2[2] = (byte)((qtetrans >> 16) & 0xff);
						pBytes2[3] = (byte)((qtetrans >> 24) & 0xff);
						Buffer.BlockCopy(pBytes, 8 + ti * MAX_SERIAL_WRITE_AT_ONCE, pBytes2, 4, qtetrans);
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
				byte[] pBytes2 = new byte[N_CTRL_CHARS + MAX_SERIAL_WRITE_AT_ONCE]; // 4 pour la synchro
				int remainTrans = nBytes; // la totalité - les bytes de synchro
				try
				{
					// premier transfert
					for (int i = 0; i < N_CTRL_CHARS; i++) pBytes2[i] = CtrlCharacters[i];
					int qtetransf = Math.Min(MAX_SERIAL_WRITE_AT_ONCE - N_CTRL_CHARS, remainTrans );
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
						System.Threading.Thread.Sleep(Math.Min(20, 1000 / (BaudRate / ((qtetransf + N_INTERMEDIATE_CTR_CHARS )* 8))));
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
				IsAvailable = Scom_Connect(portName, ref rx, ref ry);
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
				Scom_Disconnect(hCOM);
				hCOM = 0;
				ResetPalettes();
			}

			Opened = false;
			return true;
		}

	}
}

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// Output target for ZeDMD devices.
	/// </summary>
	/// Inspired from PinDMD3 code
	public class ZeDMD : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IRgb24Destination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "ZeDMD";
		public bool IsAvailable { get; private set; }

		public int Delay { get; set; } = 100;
		public int DmdWidth { get; private set; }
		public int DmdHeight { get; private set; }
		public bool DmdAllowHdScaling { get; set; } = true;
		
		// We get a DMD_ESP32 instance to communicate with ESP32
		private readonly DMDESP32.DMD_ESP32 pDMD = new DMDESP32.DMD_ESP32();

		public string Firmware { get; private set; }

		private static ZeDMD _instance;
		private readonly byte[] _frameBufferRgb24;
		private readonly byte[] _frameBufferGray4;
		private readonly byte[] _frameBufferGray2;
		private readonly byte[] _frameBufferColoredGray4;
		private readonly byte[] _frameBufferColoredGray6;
		private readonly bool isRLE = false;
		private int prevLength=0;

		private Color[] _currentPalette = ColorUtil.GetPalette(new[] { Colors.Black, Colors.OrangeRed }, 4);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Returns the current instance of the PinDMD API.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMD GetInstance()
		{
			if (_instance == null)
			{
				_instance = new ZeDMD();
			}
			else _instance.Init();
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private ZeDMD()
		{
			Init();
			// Different buffers according the type of colors we transfer
			// 4 control bytes + 4 bytes per pixel (pour le RLE)
			_frameBufferRgb24 = new byte[1 + DmdWidth * DmdHeight * 4];
			_frameBufferRgb24[0] = 3; // render RGB24, switch to 13 if RLE

			// 4 color (*3 bytes), 4 bits per pixel
			_frameBufferGray4 = new byte[1 + 12 + DmdWidth * DmdHeight / 2];
			_frameBufferGray4[0] = 7; // render compressed 4 pixels/byte with 4 colors palette

			// 4 color (*3 bytes), 2 bits per pixel
			_frameBufferGray2 = new byte[1 + 12 + DmdWidth * DmdHeight / 4];
			_frameBufferGray2[0] = 8; // render compressed 2 pixels/byte with 4 colors palette

			// 16 color (*3 bytes), 4 bits per pixel
			_frameBufferColoredGray4 = new byte[1 + 48 + DmdWidth * DmdHeight / 2];
			_frameBufferColoredGray4[0] = 9; // render compressed 2 pixels/byte with 16 colors palette

			// 64 color (*3 bytes), 6 bits per pixel
			_frameBufferColoredGray6 = new byte[1 + 192 + DmdWidth * DmdHeight * 6 / 8];
			_frameBufferColoredGray6[0] = 11; // render compressed 1 pixel/6bit with 64 colors palette

			ClearColor();
		}
		public void Init()
		{
			int rx = new int();
			int ry = new int();
			IsAvailable = (pDMD.Scom_Open(ref rx,ref ry) == 1);

			if (!IsAvailable)
			{
				Logger.Info(Name + " device not found");
				return;
			}

			DmdWidth = rx;
			DmdHeight = ry;

			Logger.Info(Name + " device found on port " + pDMD.nCOM + " with a resolution of " + DmdWidth + "x" + DmdHeight + " LEDs");
		}

		public void RenderGray2(byte[] frame)
		{
			RenderGray4(FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x1, 0x4, 0xf }));
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			// update palette
			WritePalette(frame.Palette);

			// copy to frame buffer
			var changed = FrameUtil.Copy(frame.Planes, _frameBufferGray2, 13);

			// send frame buffer to device
			if (changed)
			{
				  RenderRaw(_frameBufferGray2);
			}
		}

		public void RenderGray4(byte[] frame)
		{
			// split to sub frames
			var planes = FrameUtil.Split(DmdWidth, DmdHeight, 4, frame);

			// copy to frame buffer
			var changed = FrameUtil.Copy(planes, _frameBufferGray4, 13);

			// send frame buffer to device
			if (changed)
			{
				RenderRaw(_frameBufferGray4);
			}
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			// copy palette
			var paletteChanged = false;
			for (var i = 0; i < 16; i++)
			{
				var color = frame.Palette[i];
				var j = i * 3 + 1;
				paletteChanged = paletteChanged || (_frameBufferColoredGray4[j] != color.R || _frameBufferColoredGray4[j + 1] != color.G || _frameBufferColoredGray4[j + 2] != color.B);
				_frameBufferColoredGray4[j] = color.R;
				_frameBufferColoredGray4[j + 1] = color.G;
				_frameBufferColoredGray4[j + 2] = color.B;
			}

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBufferColoredGray4, 49);

			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				RenderRaw(_frameBufferColoredGray4);
			}
		}
		public void RenderColoredGray6(ColoredFrame frame)
		{
			// copy palette
			var paletteChanged = false;
			for (var i = 0; i < 64; i++)
			{
				var color = frame.Palette[i];
				var j = i * 3 + 1;
				paletteChanged = paletteChanged || (_frameBufferColoredGray6[j] != color.R || _frameBufferColoredGray6[j + 1] != color.G || _frameBufferColoredGray6[j + 2] != color.B);
				_frameBufferColoredGray6[j] = color.R;
				_frameBufferColoredGray6[j + 1] = color.G;
				_frameBufferColoredGray6[j + 2] = color.B;
			}

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBufferColoredGray6, 193);

			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				RenderRaw(_frameBufferColoredGray6);
			}
		}

		private bool EncodeRLERGB24(byte[] frame, byte[] dest, int offset, out int buflen)
		{
			byte[] tpframeBufferRgb24 = new byte[DmdWidth * DmdHeight * 4];
			int tpbuflen = 0;
			byte acR, acG, acB;
			byte aclen;
			int i = 0;
			while (i < DmdHeight * DmdWidth) 
			{
				acR = frame[i * 3];
				acG = frame[i * 3 + 1];
				acB = frame[i * 3 + 2];
				aclen = 1;
				i++;
				while ((aclen < 255) && (i < DmdHeight * DmdWidth) && (frame[i * 3] == acR) && (frame[i * 3 + 1] == acG) && (frame[i * 3 + 2] == acB)) 
				{
					aclen++;
					i++;
				}
				tpframeBufferRgb24[tpbuflen] = aclen;
				tpframeBufferRgb24[tpbuflen + 1] = acR;
				tpframeBufferRgb24[tpbuflen + 2] = acG;
				tpframeBufferRgb24[tpbuflen + 3] = acB;
				tpbuflen+=4;
			}
			buflen = tpbuflen;
			bool identical;
			if (tpbuflen != prevLength)
			{
				identical = false;
				prevLength = tpbuflen;
			}
			else identical = FrameUtil.CompareBuffers(dest, offset, tpframeBufferRgb24, 0, frame.Length);
			if (!identical) Buffer.BlockCopy(tpframeBufferRgb24, 0, dest, offset, tpbuflen);
			return !identical;
		}
		public void RenderRgb24(byte[] frame)
		{
			bool changed;
			// can directly be sent to the device.
			if (!isRLE)
			{
				_frameBufferRgb24[0] = 3;
				// copy data to frame buffer
				changed = FrameUtil.Copy(frame, _frameBufferRgb24, 1);
				if (changed)
				{
					pDMD.Scom_SendBytes2(_frameBufferRgb24, DmdWidth * DmdHeight * 3 + 1);
				}
			}
			else
			{
				_frameBufferRgb24[0] = 13;
				int buflen;
				changed = EncodeRLERGB24(frame, _frameBufferRgb24, 5, out buflen);
				_frameBufferRgb24[1] = (byte)(buflen & 0xff);
				_frameBufferRgb24[2] = (byte)((buflen >> 8) & 0xff);
				_frameBufferRgb24[3] = (byte)((buflen >> 16) & 0xff);
				_frameBufferRgb24[4] = (byte)((buflen >> 24) & 0xff);
				if (changed)
				{
					pDMD.Scom_SendBytes3(_frameBufferRgb24, buflen + 5);
				}
			}
		}
		public void RenderRaw(byte[] data)
		{
			if (pDMD.Opened)
			{
				pDMD.Scom_SendBytes2(data, data.Length);
			}
		}

		public void ClearDisplay()
		{
			for (var i = 4; i < _frameBufferRgb24.Length - 1; i++)
			{
				_frameBufferRgb24[i] = 0;
			}
			var tempbuf = new byte[DMDESP32.DMD_ESP32.N_CTRL_CHARS+1];
			for (int ti=0;ti< DMDESP32.DMD_ESP32.N_CTRL_CHARS;ti++) tempbuf[ti] = DMDESP32.DMD_ESP32.CtrlCharacters[ti];
			tempbuf[DMDESP32.DMD_ESP32.N_CTRL_CHARS] = 10; // clear screen
			pDMD.Scom_SendBytes(tempbuf,tempbuf.Length);
		}

		public void SetColor(Color color)
		{
			_currentPalette = ColorUtil.GetPalette(new[] { Colors.Black, color }, 4);
			WritePalette(_currentPalette);
		}

		public void SetPalette(Color[] colors, int index = -1)
		{
			_currentPalette = ColorUtil.GetPalette(colors, 4);
			WritePalette(_currentPalette);
		}

		private void WritePalette(Color[] palette)
		{
			var pos = 1;
			for (var i = 0; i < 4; i++)
			{
				_frameBufferGray2[pos] = palette[i].R;
				_frameBufferGray4[pos] = palette[i].R;
				_frameBufferGray2[pos + 1] = palette[i].G;
				_frameBufferGray4[pos + 1] = palette[i].G;
				_frameBufferGray2[pos + 2] = palette[i].B;
				_frameBufferGray4[pos + 2] = palette[i].B;
				pos += 3;
			}
		}

		public void ClearPalette()
		{
			ClearColor();
		}

		public void ClearColor()
		{
			SetColor(RenderGraph.DefaultColor);
		}

		public void Dispose()
		{
			pDMD.Scom_Close();
		}
	}
}

