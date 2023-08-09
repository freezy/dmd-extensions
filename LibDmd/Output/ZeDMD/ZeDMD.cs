using System.Linq;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// So you need 2 64x32 pannels compatible (check the Readme.md in the link above), the main constraint is to have a 1/16 scan.
	/// To build the ZeDMD HD version you either need 2 128x64 or 4 64x54 panels.
	/// Check "ZeDMD Project Page" (https://github.com/zesinger/ZeDMD_ESP32) for details.
	/// </summary>
	public class ZeDMD : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IRawOutput, IResizableDestination
	{
		public string Name => "ZeDMD";
		public bool IsAvailable { get; private set; }
		public bool NeedsDuplicateFrames => false;

		public int Delay { get; set; } = 100;
		
		public Dimensions FixedSize { get; private set; } = new Dimensions(128, 32);
		
		// We get a DMD_ESP32 instance to communicate with ESP32
		private readonly ZeDMDComm pDMD = new ZeDMDComm();

		private static ZeDMD _instance;
		private byte[] _frameBuffer;

		private const byte RGB24 = 3;
		private const byte ColGray6 = 11;
		//private const byte Gray4 = 7;
		private const byte ColGray4 = 9;
		private const byte Gray2 = 8;

		private Color[] _currentPalette = ColorUtil.GetPalette(new[] { Colors.Black, Colors.OrangeRed }, 4);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const int MAX_COLOR_ROTATIONS = 8; // maximum amount of color rotations per frame

		private Dimensions RomDimensions = new Dimensions(128, 32); // The size of the frames in the rom

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
			_frameBuffer = new byte[1 + FixedSize.Surface * 4];

			if (IsAvailable) ClearColor();
		}

		public void SetDimensions(Dimensions newDim)
		{
			byte[] tdata = new byte[5];
			RomDimensions = newDim;
			tdata[0] = 2; // send dimension mode
			tdata[1] = (byte)(newDim.Width & 0xff);
			tdata[2] = (byte)((newDim.Width >> 8) & 0xff);
			tdata[3] = (byte)(newDim.Height & 0xff);
			tdata[4] = (byte)((newDim.Height >> 8) & 0xff);
			RenderRaw(tdata);
		}

		public void Init()
		{
			IsAvailable = (pDMD.Open(out var width, out var height) == 1);

			if (!IsAvailable) {
				Logger.Info(Name + " device not found");
				return;
			}

			FixedSize = new Dimensions(width, height);
			_frameBuffer = new byte[1 + FixedSize.Surface * 4];

			Logger.Info($"{Name} device found on port {pDMD.nCOM} with a resolution of {FixedSize} LEDs");
		}

		public void RenderGray2(DmdFrame frame)
		{
			frame.CopyPlanesTo(_frameBuffer, 13);

			// send frame buffer to device
			RenderRaw(_frameBuffer, Gray2, 1 + 12 + RomDimensions.Surface / 4);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			// copy palette
			CopyPalette(frame.Palette, _frameBuffer, 4);

			// copy frame
			frame.CopyPlanesTo(_frameBuffer, 13);

			// send frame buffer to device
			RenderRaw(_frameBuffer, Gray2, 1 + 12 + RomDimensions.Surface / 4);
		}

		private float CalcBrightness(float x)
		{
			// function to improve the brightness with fx=ax²+bc+c, f(0)=0, f(1)=1, f'(1.1)=0
			return (-x * x + 2.1f * x) / 1.1f;
		}

		public void RenderGray4(DmdFrame frame)
		{
			frame.CopyPlanesTo(_frameBuffer, 49);

			// copy palette
			for (int ti = 0; ti < 16; ti++)
			{
				_frameBuffer[1 + ti * 3] = (byte)(255.0f * CalcBrightness(ti / 15.0f));
				_frameBuffer[1 + ti * 3 + 1] = (byte)(109.0f * CalcBrightness(ti / 15.0f));
				_frameBuffer[1 + ti * 3 + 2] = (byte)(0.0f * CalcBrightness(ti / 15.0f));
			}

			// send frame buffer to device
			RenderRaw(_frameBuffer, ColGray4, 1 + 48 + RomDimensions.Surface / 2);
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			// copy palette
			CopyPalette(frame.Palette, _frameBuffer, 16);

			// copy frame
			frame.CopyPlanesTo(_frameBuffer, 49);

			// send frame buffer to device
			RenderRaw(_frameBuffer, ColGray4, 1 + 48 + RomDimensions.Surface / 2);
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			// copy palette
			CopyPalette(frame.Palette, _frameBuffer, 64);

			// copy frame
			frame.CopyPlanesTo(_frameBuffer, 193);

			// send frame buffer to device
			if (frame.RotateColors) {
				for (int ti = 0; ti < 3 * MAX_COLOR_ROTATIONS; ti++) {
					_frameBuffer[ti + 1 + 192 + RomDimensions.Surface * 6 / 8] = frame.Rotations[ti];
				}

			} else {
				for (int ti = 0; ti < MAX_COLOR_ROTATIONS; ti++) {
					_frameBuffer[ti * 3 + 1 + 192 + RomDimensions.Surface * 6 / 8] = 255;
				}
			}
			RenderRaw(_frameBuffer, ColGray6, 1 + 192 + 6 * RomDimensions.Surface / 8 + 3 * 8);
		}

		private static void CopyPalette(Color[] palette,byte[] framebuffer,int ncol)
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
		}

		public void RenderRgb24(DmdFrame frame)
		{
			// can directly be sent to the device.
			_frameBuffer[0] = RGB24;

			// copy data to frame buffer
			frame.CopyDataTo(_frameBuffer, 1);
			pDMD.QueueFrame(_frameBuffer.Take(RomDimensions.Surface * 3 + 1).ToArray());
		}

		private void RenderRaw(byte[] data, byte mode, int length)
		{
			data[0] = mode;
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

		public void SetPalette(Color[] colors)
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
