using System.Linq;
using System.Windows.Media;
using LibDmd.Common;
using NLog;

namespace LibDmd.Output.ZeDMD
{
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
	public class ZeDMD : IGray2Destination, IGray4Destination, IGray6Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IRgb24Destination,  IRawOutput, IResizableDestination
	{
		public string Name { get; } = "ZeDMD";
		public bool IsAvailable { get; private set; }

		public int Delay { get; set; } = 100;
		public int DmdWidth { get; private set; }
		public int DmdHeight { get; private set; }
		public bool DmdAllowHdScaling { get; set; } = true;

		// We get a DMD_ESP32 instance to communicate with ESP32
		private readonly ZeDMDComm pDMD = new ZeDMDComm();

		private static ZeDMD _instance;
		private readonly byte[] _frameBuffer;

		private const byte RGB24 = 3;
		private const byte ColGray6 = 11;
		private const byte Gray4 = 7;
		private const byte ColGray4 = 9;
		private const byte Gray2 = 8;

		private Color[] _currentPalette = ColorUtil.GetPalette(new[] { Colors.Black, Colors.OrangeRed }, 4);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const int MAX_COLOR_ROTATIONS = 8; // maximum amount of color rotations per frame

		private int RomWidth = 0; // The size of the frames in the rom
		private int RomHeight = 0;

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
			// Different modes require different buffer sizes. This one should be safe for all.
			_frameBuffer = new byte[1 + DmdWidth * DmdHeight * 4];

			if (IsAvailable) ClearColor();
		}

		public void SetDimensions(int width, int height)
		{
			byte[] tdata = new byte[5];
			RomHeight = height;
			RomWidth = width;
			tdata[0] = 2; // send dimension mode
			tdata[1] = (byte)(width & 0xff);
			tdata[2] = (byte)((width >> 8) & 0xff);
			tdata[3] = (byte)(height & 0xff);
			tdata[4] = (byte)((height >> 8) & 0xff);
			RenderRaw(tdata);
		}

		public void Init()
		{
			int width = new int();
			int height = new int();
			IsAvailable = (pDMD.Open(out width,out height) == 1);

			if (!IsAvailable)
			{
				Logger.Info(Name + " device not found");
				return;
			}

			DmdWidth = width;
			DmdHeight = height;

			Logger.Info(Name + " device found on port " + pDMD.nCOM + " with a resolution of " + DmdWidth + "x" + DmdHeight + " LEDs");
		}

		public void RenderGray2(byte[] frame)
		{
			var planes = FrameUtil.Split(RomWidth, RomHeight, 2, frame);
			var changed = FrameUtil.Copy(planes, _frameBuffer, 13);

			// send frame buffer to device
			if (changed)
			{
				RenderRaw(_frameBuffer, Gray2, 1 + 12 + RomWidth * RomHeight / 4);
			}
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			// copy palette
			var paletteChanged = CopyPalette(frame.Palette, _frameBuffer, 4);

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBuffer, 13);

			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				RenderRaw(_frameBuffer, Gray2, 1 + 12 + RomWidth * RomHeight / 4);
			}
		}

		private float CalcBrightness(float x)
		{
			// function to improve the brightness with fx=ax²+bc+c, f(0)=0, f(1)=1, f'(1.1)=0
			return (-x * x + 2.1f * x) / 1.1f;
		}

		public void RenderGray4(byte[] frame)
		{
			var planes = FrameUtil.Split(RomWidth, RomHeight, 4, frame);
			var changed = FrameUtil.Copy(planes, _frameBuffer, 49);

			// send frame buffer to device
			if (changed)
			{
				for (int ti = 0; ti < 16; ti++)
				{
					_frameBuffer[1 + ti * 3] = (byte)(255.0f * CalcBrightness(ti / 15.0f));
					_frameBuffer[1 + ti * 3 + 1] = (byte)(109.0f * CalcBrightness(ti / 15.0f));
					_frameBuffer[1 + ti * 3 + 2] = (byte)(0.0f * CalcBrightness(ti / 15.0f));
				}
				RenderRaw(_frameBuffer, ColGray4, 1 + 48 + RomWidth * RomHeight / 2);
			}
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			// copy palette
			var paletteChanged = CopyPalette(frame.Palette, _frameBuffer, 16);

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBuffer, 49);

			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				RenderRaw(_frameBuffer, ColGray4, 1 + 48 + RomWidth * RomHeight / 2);
			}
		}

		public void RenderGray6(byte[] frame)
		{
			var planes = FrameUtil.Split(RomWidth, RomHeight, 6, frame);
			var changed = FrameUtil.Copy(planes, _frameBuffer, 193);

			// send frame buffer to device
			if (changed)
			{
				RenderRaw(_frameBuffer, Gray2, 1 + 192 + 6 * RomWidth * RomHeight / 8);
			}
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			// copy palette
			var paletteChanged = CopyPalette(frame.Palette, _frameBuffer, 64);

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBuffer, 193);

			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				if (frame.RotateColors)
				{
					for (int ti = 0; ti < 3 * MAX_COLOR_ROTATIONS; ti++) _frameBuffer[ti + 1 + 192 + RomWidth * RomHeight * 6 / 8] = frame.Rotations[ti];
				}
				else
				{
					for (int ti = 0; ti < MAX_COLOR_ROTATIONS; ti++) _frameBuffer[ti * 3 + 1 + 192 + RomWidth * RomHeight * 6 / 8] = 255;
				}
				RenderRaw(_frameBuffer, ColGray6, 1 + 192 + 6 * RomWidth * RomHeight / 8 + 3 * 8);
			}
		}

		private bool CopyPalette(Color[] palette,byte[] framebuffer,int ncol)
		{
			var paletteChanged = false;
			for (var i = 0; i < ncol; i++)
			{
				var color = palette[i];
				var j = i * 3 + 1;
				paletteChanged = paletteChanged || (framebuffer[j] != color.R || framebuffer[j + 1] != color.G || framebuffer[j + 2] != color.B);
				framebuffer[j] = color.R;
				framebuffer[j + 1] = color.G;
				framebuffer[j + 2] = color.B;
			}
			return paletteChanged;
		}

		public void RenderRgb24(byte[] frame)
		{
			bool changed;
			// can directly be sent to the device.
			_frameBuffer[0] = RGB24;
			// copy data to frame buffer
			changed = FrameUtil.Copy(frame, _frameBuffer, 1);
			if (changed)
			{
				pDMD.QueueFrame(_frameBuffer.Take(RomWidth * RomHeight * 3 + 1).ToArray());
			}
		}

		public void RenderRaw(byte[] data, byte Mode, int length)
		{
			data[0] = Mode;
			if (pDMD.Opened)
			{
				pDMD.QueueFrame(data.Take(length).ToArray());
			}
		}

		public void RenderRaw(byte[] data)
		{
			if (pDMD.Opened)
			{
				pDMD.QueueFrame(data);
			}
		}

		public void ClearDisplay()
		{
			for (var i = 1; i < _frameBuffer.Length - 1; i++)
			{
				_frameBuffer[i] = 0;
			}
			byte[] tempbuf = new byte[1];
			tempbuf[0] = 10; // clear screen
			pDMD.QueueFrame(tempbuf);
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
			for (var i = 0; i < palette.Length; i++)
			{
				_frameBuffer[pos] = palette[i].R;
				_frameBuffer[pos + 1] = palette[i].G;
				_frameBuffer[pos + 2] = palette[i].B;
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
			pDMD.Close();
		}
	}
}

