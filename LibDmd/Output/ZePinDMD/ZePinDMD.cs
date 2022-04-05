using System;
using System.Windows.Media;
using LibDmd.Common;
using NLog;
using System.IO.Ports;
using System.IO;
/// <summary>
/// ZePinDMD - real DMD with LED matrix display controlled with a cheap ESP32
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
		public int BaudRate;
		public byte ByteSize = 8;
		public bool Opened = false;
		public byte Parity = 0; // 0-4=no,odd,even,mark,space
		public string Port;
		public int ReadTimeout;
		public byte StopBits = 1; // 0,1,2 = 1, 1.5, 2
		private byte[] oldbuffer = new byte[16384];
		private SerialPort _serialPort;




		// for debugging
		private StreamWriter sw;
		private uint nACK = 0;





		private const uint GENERIC_READ = 0x80000000;
		private const uint GENERIC_WRITE = 0x40000000;
		private const int INVALID_HANDLE_VALUE = -1;
		private const int OPEN_EXISTING = 3;

		private bool Scom_Connect(string port)
		{
			try
			{
				BaudRate = 921600;
				Port = port;
				_serialPort = new SerialPort(port, BaudRate, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
				_serialPort.ReadTimeout = 100;
				_serialPort.WriteTimeout = 100;
				_serialPort.Open();
				_serialPort.Write(new byte[] { 0x81, 0xC3, 0xE7, 15 }, 0, 4);
				System.Threading.Thread.Sleep(100);
				var result = new byte[4];
				_serialPort.Read(result, 0, 4);
				if ((result[0] == 0x81) && (result[1] == 0xC3) && (result[2] == 0xE7) && (result[3] == 15))
				{
					return true;
				}
			}
			catch (Exception e)
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
			if (_serialPort != null && _serialPort.IsOpen)
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




					nACK++;
					if (nACK < 50) sw.WriteLine(nACK.ToString() + ": Action " + pBytes[3].ToString() + " with " + (nBytes - 4).ToString() + "bytes sent");




					System.Threading.Thread.Sleep(50);
					var result = new byte[4];
					_serialPort.Read(result, 0, 4);
					if ((result[0] == 0x81) && (result[1] == 0xC3) && (result[2] == 0xE7) && (result[3] == 15))
					{




						if (nACK < 50) sw.WriteLine("Got a COMPLETE ACK");




					}
					return true;
				}
				catch { };
			}
			return false;
		}
		public bool Scom_SendBytes2(byte[] pBytes, int nBytes,int nBPerTrans)
		{
			if (_serialPort.IsOpen)
			{
				byte[] pBytes2 = new byte[nBPerTrans];
				int ti = 0;
				int remainTrans = nBytes;




				nACK++;
				if (nACK < 50) sw.WriteLine(nACK.ToString() + ": Action " + pBytes[3].ToString() + " with " + (nBytes - 4).ToString() + "bytes multi-sent");




				try
				{
					while (remainTrans>0)
					{
						Buffer.BlockCopy(pBytes,ti * nBPerTrans, pBytes2, 0, Math.Min(remainTrans,nBPerTrans));
						_serialPort.Write(pBytes2, 0, nBPerTrans);
						System.Threading.Thread.Sleep(50);
						var result = new byte[4];
						_serialPort.Read(result, 0, 4);
						if ((result[0] == 0x81) && (result[1] == 0xC3) && (result[2] == 0xE7) && (result[3] == 15))
						{




							if (nACK < 50) sw.WriteLine("Got an ACK after a transfer");




						}
						remainTrans -= nBPerTrans;
						ti++;
					}
					return true;
				}
				catch { };
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
		}
		public int Scom_Open()
		{




			sw = File.CreateText("E:\\ZePinDMD_log.txt");
			
			
			
			
			bool IsAvailable = false;
			var ports = SerialPort.GetPortNames();
			foreach (var portName in ports)
			{
				IsAvailable = Scom_Connect(portName);
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
				sw.Close();
			}

			Opened = false;
			return true;
		}

	}
}

namespace LibDmd.Output.ZePinDMD
{
	/// <summary>
	/// Output target for ZePinDMD devices.
	/// </summary>
	/// Inspired from PinDMD3 code
	public class ZePinDMD : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IRgb24Destination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "ZePinDMD";
		public bool IsAvailable { get; private set; }

		public int Delay { get; set; } = 100;
		public int DmdWidth { get; } = 128;
		public int DmdHeight { get; } = 32;
		public bool DmdAllowHdScaling { get; set; } = true;
		
		// We get a DMD_ESP32 instance to communicate with ESP32
		private readonly DMDESP32.DMD_ESP32 pDMD = new DMDESP32.DMD_ESP32();

		public string Firmware { get; private set; }

		private static ZePinDMD _instance;
		private readonly byte[] _frameBufferRgb24;
		private readonly byte[] _frameBufferGray4;
		private readonly byte[] _frameBufferGray2;
		private readonly byte[] _frameBufferColoredGray4;
		private readonly byte[] _frameBufferColoredGray6;

		private Color[] _currentPalette = ColorUtil.GetPalette(new[] { Colors.Black, Colors.OrangeRed }, 4);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Returns the current instance of the PinDMD API.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZePinDMD GetInstance()
		{
			if (_instance == null)
			{
				_instance = new ZePinDMD();
			}
			_instance.Init();
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private ZePinDMD()
		{
			// Different buffers according the type of colors we transfer
			// 4 control bytes + 3 bytes per pixel
			_frameBufferRgb24 = new byte[4 + DmdWidth * DmdHeight * 3];
			_frameBufferRgb24[0] = 0x81;
			_frameBufferRgb24[1] = 0xC3;
			_frameBufferRgb24[2] = 0xE7;
			_frameBufferRgb24[3] = 3; // render RGB24

			// 4 control bytes, 4 color (*3 bytes), 4 bits per pixel
			_frameBufferGray4 = new byte[4 + 12 + DmdWidth * DmdHeight / 2];
			_frameBufferGray4[0] = 0x81;
			_frameBufferGray4[1] = 0xC3;
			_frameBufferGray4[2] = 0xE7;
			_frameBufferGray4[3] = 7; // render compressed 4 pixels/byte with 4 colors palette

			// 4 control bytes, 4 color (*3 bytes), 2 bits per pixel
			_frameBufferGray2 = new byte[4 + 12 + DmdWidth * DmdHeight / 4];
			_frameBufferGray2[0] = 0x81;
			_frameBufferGray2[1] = 0xC3;
			_frameBufferGray2[2] = 0xE7;
			_frameBufferGray2[3] = 8; // render compressed 2 pixels/byte with 4 colors palette

			// 4 control bytes, 16 color (*3 bytes), 4 bits per pixel
			_frameBufferColoredGray4 = new byte[4 + 48 + DmdWidth * DmdHeight / 2];
			_frameBufferColoredGray4[0] = 0x81;
			_frameBufferColoredGray4[1] = 0xC3;
			_frameBufferColoredGray4[2] = 0xE7;
			_frameBufferColoredGray4[3] = 9; // render compressed 2 pixels/byte with 16 colors palette

			// 4 control bytes, 64 color (*3 bytes), 6 bits per pixel
			_frameBufferColoredGray6 = new byte[4 + 192 + DmdWidth * DmdHeight * 6 / 8];
			_frameBufferColoredGray6[0] = 0x81;
			_frameBufferColoredGray6[1] = 0xC3;
			_frameBufferColoredGray6[2] = 0xE7;
			_frameBufferColoredGray6[3] = 11; // render compressed 1 pixel/6bit with 64 colors palette


			ClearColor();
		}
		public void Init()
		{
			IsAvailable = (pDMD.Scom_Open() == 1);

			if (!IsAvailable)
			{
				Logger.Info("ZePinDMD device not found.");
				return;
			}
		}

		public void RenderGray2(byte[] frame)
		{
			// split to sub frames
			var planes = FrameUtil.Split(DmdWidth, DmdHeight, 2, frame);

			// copy to frame buffer
			var changed = FrameUtil.Copy(planes, _frameBufferGray2, 16);

			// send frame buffer to device
			if (changed)
			{
				WritePalette(_currentPalette);
				RenderRaw(_frameBufferGray2);
			}
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			// update palette
			WritePalette(frame.Palette);

			// copy to frame buffer
			var changed = FrameUtil.Copy(frame.Planes, _frameBufferGray2, 16);

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
			var changed = FrameUtil.Copy(planes, _frameBufferGray4, 16);

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
				var j = i * 3 + 4;
				paletteChanged = paletteChanged || (_frameBufferColoredGray4[j] != color.R || _frameBufferColoredGray4[j + 1] != color.G || _frameBufferColoredGray4[j + 2] != color.B);
				_frameBufferColoredGray4[j] = color.R;
				_frameBufferColoredGray4[j + 1] = color.G;
				_frameBufferColoredGray4[j + 2] = color.B;
			}

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBufferColoredGray4, 52);

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
				var j = i * 3 + 4;
				paletteChanged = paletteChanged || (_frameBufferColoredGray6[j] != color.R || _frameBufferColoredGray6[j + 1] != color.G || _frameBufferColoredGray6[j + 2] != color.B);
				_frameBufferColoredGray6[j] = color.R;
				_frameBufferColoredGray6[j + 1] = color.G;
				_frameBufferColoredGray6[j + 2] = color.B;
			}

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBufferColoredGray6, 196);

			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				RenderRaw(_frameBufferColoredGray6);
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			// copy data to frame buffer
			var changed = FrameUtil.Copy(frame, _frameBufferRgb24, 4);
			// can directly be sent to the device.
			if (changed)
			{
				pDMD.Scom_SendBytes2(_frameBufferRgb24, _frameBufferRgb24.Length, 132 * 16 * 3 + 4);
			}
		}
		public void RenderRaw(byte[] data)
		{
			if (pDMD.Opened)
			{
				pDMD.Scom_SendBytes(data, data.Length);
			}
		}

		public void ClearDisplay()
		{
			for (var i = 4; i < _frameBufferRgb24.Length - 1; i++)
			{
				_frameBufferRgb24[i] = 0;
			}
			var tempbuf = new byte[4];
			tempbuf[0] = 0x81;
			tempbuf[1] = 0xC3;
			tempbuf[2] = 0xE7;
			tempbuf[3] = 10; // clear screen
			RenderRaw(tempbuf);
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
			var pos = 4;
			for (var i = 0; i < 4; i++)
			{
				_frameBufferGray2[pos] = palette[3 - i].R;
				_frameBufferGray4[pos] = palette[3 - i].R;
				_frameBufferGray2[pos + 1] = palette[3 - i].G;
				_frameBufferGray4[pos + 1] = palette[3 - i].G;
				_frameBufferGray2[pos + 2] = palette[3 - i].B;
				_frameBufferGray4[pos + 2] = palette[3 - i].B;
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

