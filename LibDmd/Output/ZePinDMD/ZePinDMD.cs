using System;
using System.Windows.Media;
using LibDmd.Common;
using NLog;
using System.Runtime.InteropServices;

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
		public int PortNum;
		public int ReadTimeout;
		public byte StopBits = 1; // 0,1,2 = 1, 1.5, 2
		private byte[] oldbuffer = new byte[16384];
		// here you can define new ESP32 devices giving the Windows registry subdirectory of "SYSTEM\CurrentControlSet\Enum\USB\"
		// where the key "FriendlyName" contain the "COMx" number in it
		// increase the N_COMPATIBLE_DEVICES and add the string of the subdirectory in VIDPID
		private int N_COMPATIBLE_DEVICES = 2;
		private static readonly string[] VIDPID = { "VID_10C4&PID_EA60\\0001", "VID_10C4&PID_EA70\\0001" };

		// We import a lot of functions and structures from C to access Windows registry, serial ports and direct memory access
		[StructLayout(LayoutKind.Sequential)]
		private struct DCB
		{
			//taken from c struct in platform sdk
			public int DCBlength; // sizeof(DCB)
			public int BaudRate; // current baud rate

			public int fBinary; // binary mode, no EOF check
			public int fParity; // enable parity checking
			public int fOutxCtsFlow; // CTS output flow control
			public int fOutxDsrFlow; // DSR output flow control
			public int fDtrControl; // DTR flow control type
			public int fDsrSensitivity; // DSR sensitivity
			public int fTXContinueOnXoff; // XOFF continues Tx
			public int fOutX; // XON/XOFF out flow control
			public int fInX; // XON/XOFF in flow control
			public int fErrorChar; // enable error replacement
			public int fNull; // enable null stripping
			public int fRtsControl; // RTS flow control
			public int fAbortOnError; // abort on error
			public int fDummy2; // reserved
			public uint flags;
			public ushort wReserved; // not currently used
			public ushort XonLim; // transmit XON threshold
			public ushort XoffLim; // transmit XOFF threshold
			public byte ByteSize; // number of bits/byte, 4-8
			public byte Parity; // 0-4=no,odd,even,mark,space
			public byte StopBits; // 0,1,2 = 1, 1.5, 2
			public char XonChar; // Tx and Rx XON character
			public char XoffChar; // Tx and Rx XOFF character
			public char ErrorChar; // error replacement character
			public char EofChar; // end of input character
			public char EvtChar; // received event character
			public ushort wReserved1; // reserved; do not use
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct COMSTAT
		{
			public uint fCtsHold;
			public uint fDsrHold;
			public uint fRlsdHold;
			public uint fXoffHold;
			public uint fXoffSent;
			public uint fEof;
			public uint fTxim;
			public uint fReserved;
			public uint cbInQue;
			public uint cbOutQue;
		}
		[StructLayout(LayoutKind.Sequential)]
		private struct COMMTIMEOUTS
		{
			public int ReadIntervalTimeout;
			public int ReadTotalTimeoutMultiplier;
			public int ReadTotalTimeoutConstant;
			public int WriteTotalTimeoutMultiplier;
			public int WriteTotalTimeoutConstant;
		}

		[DllImport("kernel32.dll")]
		private static extern bool CloseHandle(
			int hObject // handle to object
			);

		[DllImport("kernel32.dll")]
		private static extern int CreateFile(
			string lpFileName, // file name
			uint dwDesiredAccess, // access mode
			int dwShareMode, // share mode
			int lpSecurityAttributes, // SD
			int dwCreationDisposition, // how to create
			int dwFlagsAndAttributes, // file attributes
			int hTemplateFile // handle to template file
			);

		[DllImport("kernel32.dll")]
		private static extern bool GetCommState(
			int hFile, // handle to communications device
			ref DCB lpDCB // device-control block
			);

		[DllImport("kernel32.dll")]
		private static extern bool GetCommTimeouts(
			int hFile, // handle to comm device
			ref COMMTIMEOUTS lpCommTimeouts // time-out values
			);

		[DllImport("kernel32.dll")]
		private static extern uint GetLastError();

		[DllImport("kernel32.dll")]
		private static extern bool ReadFile(
			int hFile, // handle to file
			byte[] lpBuffer, // data buffer
			uint nNumberOfBytesToRead, // number of bytes to read
			ref uint lpNumberOfBytesRead, // number of bytes read
			IntPtr lpOverlapped // overlapped buffer
			);

		[DllImport("kernel32.dll")]
		private static extern bool SetCommState(
			int hFile, // handle to communications device
			ref DCB lpDCB // device-control block
			);

		[DllImport("kernel32.dll")]
		private static extern bool SetCommTimeouts(
			int hFile, // handle to comm device
			ref COMMTIMEOUTS lpCommTimeouts // time-out values
			);

		[DllImport("kernel32.dll")]
		private static extern bool ClearCommError(
			int hFile, // handle to comm device
			IntPtr lpErrors, // time-out values
			ref COMSTAT lpstat
			);

		[DllImport("kernel32.dll")]
		unsafe private static extern bool WriteFile(
			int hFile, // handle to file
			byte* lpBuffer, // data buffer
			uint nNumberOfBytesToWrite, // number of bytes to write
			ref uint lpNumberOfBytesWritten, // number of bytes written
			IntPtr lpOverlapped // overlapped buffer
			);

		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		unsafe private static extern int memcmp(byte* b1, byte* b2, uint count);

		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		unsafe private static extern IntPtr memcpy(byte* b1, byte* b2, uint count);

		[DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
		private static extern int RegOpenKeyEx(uint hKey, string subKey, int ulOptions, int samDesired, out uint hkResult);
		[DllImport("Advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern Int32 RegGetValue(uint hkey, string lpSubKey, string lpValue, uint dwFlags, uint pdwType, char[] pvData, ref UInt32 pcbData);
		public struct RGB24
		{
			public byte red;
			public byte green;
			public byte blue;
		};

		private const uint GENERIC_READ = 0x80000000;
		private const uint GENERIC_WRITE = 0x40000000;
		private const int INVALID_HANDLE_VALUE = -1;
		private const int OPEN_EXISTING = 3;

		private int Scom_Connect(int portnum, int baudrate)
		{
			BaudRate = baudrate;
			PortNum = portnum;
			DCB dcbCommPort = new DCB();
			hCOM = CreateFile("COM" + PortNum, GENERIC_READ | GENERIC_WRITE, 0, 0, OPEN_EXISTING, 0, 0);
			if (hCOM == INVALID_HANDLE_VALUE) return 0;
			GetCommState(hCOM, ref dcbCommPort);
			dcbCommPort.BaudRate = BaudRate;
			dcbCommPort.flags = 0;
			dcbCommPort.fDtrControl = 1;
			dcbCommPort.flags |= 1;
			if (Parity > 0)
			{
				dcbCommPort.flags |= 2;
			}
			dcbCommPort.Parity = Parity;
			dcbCommPort.ByteSize = ByteSize;
			dcbCommPort.StopBits = StopBits;
			if (!SetCommState(hCOM, ref dcbCommPort)) return 0;
			Opened = true;
			return 1;
		}
		private void Scom_Disconnect(int comhandle)
		{
			CloseHandle(comhandle);
		}
		public bool Scom_SendBytes(byte[] pBytes, uint nBytes)
		{
			uint bytesSend = new uint();
			COMSTAT status = new COMSTAT();
			unsafe
			{
				fixed (byte* pOct = pBytes)
				{
					if (!WriteFile(hCOM, pOct, nBytes, ref bytesSend, IntPtr.Zero))
					{
						ClearCommError(hCOM, IntPtr.Zero, ref status);
						return false;
					}
				}
			}
			return true;
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
		private bool Scom_findESP32_COM(out int pCOMNB)
		{
			uint i = new uint();
			uint cchValue = new uint();
			uint hKey = new uint();
			for (i = 0; i < N_COMPATIBLE_DEVICES; i++)
			{
				string tpbuf = "SYSTEM\\CurrentControlSet\\Enum\\USB\\" + VIDPID[i];
				if (RegOpenKeyEx((uint)0x80000002, tpbuf, 0, (0x00020000 | 0x0001 | 0x0008 | 0x0010) & (~0x00100000), out hKey) == 0)
				{
					cchValue = 16383;
					char[] tpbuf2 = new char[260];
					if (RegGetValue(hKey, "", "FriendlyName", 0x0000ffff, 0, tpbuf2, ref cchValue) == 0)
					{
						string tpbuf3 = new string(tpbuf2);
						int idx = tpbuf3.IndexOf("COM");
						pCOMNB = int.Parse(tpbuf3[idx + 3].ToString());
						return true;
					}
				}
			}
			pCOMNB = 0;
			return false;
		}

		public int Scom_Open()
		{
			int COMPort = new int();
			if (!Scom_findESP32_COM(out COMPort)) return 0;
			int res = Scom_Connect(COMPort, 921600);
			if (res == 0) return 0;
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

namespace LibDmd.Output.ZePinDMD
{
	/// <summary>
	/// Output target for ZePinDMD devices.
	/// </summary>
	/// Inspired from PinDMD3 code
	public class ZePinDMD : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IRawOutput, IFixedSizeDestination
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
			_frameBufferColoredGray4[3] = 9; // render compressed 4 pixels/byte with 16 colors palette


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

		public void RenderRgb24(byte[] frame)
		{
			// copy data to frame buffer
			var changed = FrameUtil.Copy(frame, _frameBufferRgb24, 1);

			// can directly be sent to the device.
			if (changed)
			{
				RenderRaw(_frameBufferRgb24);
			}
		}
		public void RenderRaw(byte[] data)
		{
			if (pDMD.Opened)
			{
				pDMD.Scom_SendBytes(data, (uint)data.Length);
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

