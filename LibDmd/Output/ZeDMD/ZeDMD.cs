//using System.Threading;
using System.Windows.Media;
//using System.Windows.Navigation;
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
	public class ZeDMD : IGray4Destination, IGray2Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IRgb24Destination,  IRawOutput, IFixedSizeDestination 
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
		private readonly byte[] _frameBufferRgb24;
		/*private readonly byte[] _frameBufferGray4;
		private readonly byte[] _frameBufferGray2;
		private readonly byte[] _frameBufferColoredGray4;
		private readonly byte[] _frameBufferColoredGray6;*/

		private const byte RGB24 = 3;
		private const byte ColGray6 = 11;
		private const byte Gray4 = 7;
		private const byte ColGray4 = 9;
		//private const byte Gray2 = 8;

		private Color[] _currentPalette = ColorUtil.GetPalette(new[] { Colors.Black, Colors.OrangeRed }, 4);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const int MAX_COLOR_ROTATIONS = 8; // maximum amount of color rotations per frame

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
			// Different buffers according to the type of colors we transfer
			// 4 control bytes + 4 bytes per pixel (for RLE)
			_frameBufferRgb24 = new byte[1 + DmdWidth * DmdHeight * 4];
			//_frameBufferRgb24[0] = RGB24; // render RGB24

			// 4 color (*3 bytes), 4 bits per pixel
			/*_frameBufferGray4 = new byte[1 + 48 + DmdWidth * DmdHeight];/// 2];
			_frameBufferGray4[0] = Gray4; // render compressed 4 pixels/byte with 4 colors palette

			// 4 color (*3 bytes), 2 bits per pixel
			_frameBufferGray2 = new byte[1 + 12 + DmdWidth * DmdHeight];/// 4];
			_frameBufferGray2[0] = Gray2; // render compressed 2 pixels/byte with 4 colors palette

			// 16 color (*3 bytes), 4 bits per pixel
			_frameBufferColoredGray4 = new byte[1 + 48 + DmdWidth * DmdHeight];// / 2];
			_frameBufferColoredGray4[0] = ColGray4; // render compressed 2 pixels/byte with 16 colors palette

			// 64 color (*3 bytes), 6 bits per pixel
			_frameBufferColoredGray6 = new byte[1 + 192 + DmdWidth * DmdHeight];// * 6 / 8];
			_frameBufferColoredGray6[0] = ColGray6; // render compressed 1 pixel/6bit with 64 colors palette*/

			ClearColor();
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
			RenderGray4(FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x1, 0x4, 0xf }));
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			byte[][] tpl = new byte[4][];
			for (int i = 0; i < 4; i++) tpl[i] = new byte[DmdHeight * DmdWidth / 8];
			for (int i = 0; i < DmdHeight * DmdWidth / 8; i++) 
			{
				tpl[0][i] = frame.Planes[0][i];
				tpl[1][i] = frame.Planes[1][i];
				tpl[2][i] = tpl[3][i] = 0;
			}
			Color[] tpa = new Color[16]{ Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0),
										 Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0),
										 Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0),
										 Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0), Color.FromRgb(0, 0, 0)};
			for (int i = 0; i < 4; i++) tpa[i] = frame.Palette[i];
			var tfr = new ColoredFrame(tpl, tpa);
			RenderColoredGray4(tfr);
			// copy palette
			/*var paletteChanged = CopyPalette(frame.Palette, _frameBufferGray2, 4);

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBufferGray2, 13);
			Thread.Sleep(100);
			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				RenderRaw(_frameBufferGray2);
			}*/
			/*SetPalette(frame.Palette, frame.PaletteIndex);

			var joinedFrame = FrameUtil.Join(DmdWidth, DmdHeight, frame.Planes);

			// send frame buffer to device
			RenderGray4(FrameUtil.ConvertGrayToGray(joinedFrame, new byte[] { 0x0, 0x1, 0x4, 0xf }));*/
		}

		public void RenderGray4(byte[] frame)
		{
			// split to sub frames
			var planes = FrameUtil.Split(DmdWidth, DmdHeight, 4, frame);

			// copy to frame buffer
			//var changed = FrameUtil.Copy(planes, _frameBufferGray4, 13);
			var changed = FrameUtil.Copy(planes, _frameBufferRgb24, 13);

			// send frame buffer to device
			if (changed)
			{
				RenderRaw(_frameBufferRgb24, Gray4, 1 + 48 + DmdWidth * DmdHeight / 2);
			}
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			// copy palette
			var paletteChanged = CopyPalette(frame.Palette, _frameBufferRgb24, 16);

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBufferRgb24, 49);

			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				RenderRaw(_frameBufferRgb24, ColGray4, 1 + 48 + DmdWidth * DmdHeight/ 2);
			}
		}
		public void RenderColoredGray6(ColoredFrame frame)
		{
			// copy palette
			var paletteChanged = CopyPalette(frame.Palette, _frameBufferRgb24, 64);

			// copy frame
			var frameChanged = FrameUtil.Copy(frame.Planes, _frameBufferRgb24, 193);

			// send frame buffer to device
			if (frameChanged || paletteChanged)
			{
				if (frame.isRotation)
				{
					for (int ti = 0; ti < 3 * MAX_COLOR_ROTATIONS; ti++) _frameBufferRgb24[ti + 1 + 192 + DmdWidth * DmdHeight * 6 / 8] = frame.Rotations[ti];
				}
				else
				{
					for (int ti = 0; ti < MAX_COLOR_ROTATIONS; ti++) _frameBufferRgb24[ti * 3 + 1 + 192 + DmdWidth * DmdHeight * 6 / 8] = 255;
				}
				RenderRaw(_frameBufferRgb24, ColGray6, 1 + 192 + DmdWidth * DmdHeight * 6 / 8 + 3 * 8);
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
				_frameBufferRgb24[0] = RGB24;
				// copy data to frame buffer
				changed = FrameUtil.Copy(frame, _frameBufferRgb24, 1);
				if (changed)
				{
					pDMD.StreamBytes(_frameBufferRgb24, DmdWidth * DmdHeight * 3 + 1);
				}
		}
		public void RenderRaw(byte[] data, byte Mode, int length)
		{
			data[0] = Mode;
			if (pDMD.Opened)
			{
				pDMD.StreamBytes(data, length);
			}
		}

		public void RenderRaw(byte[] data)
		{



			//return;
			
			
			
			if (pDMD.Opened)
			{
				pDMD.StreamBytes(data, data.Length);
			}
		}

		public void ClearDisplay()
		{
			for (var i = 1; i < _frameBufferRgb24.Length - 1; i++)
			{
				_frameBufferRgb24[i] = 0;
			}
			byte[] tempbuf = new byte[1];
			tempbuf[0] = 10; // clear screen
			pDMD.StreamBytes(tempbuf, 1);
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
				_frameBufferRgb24[pos] = palette[i].R;
				_frameBufferRgb24[pos + 1] = palette[i].G;
				_frameBufferRgb24[pos + 2] = palette[i].B;
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

