using System.Windows.Media;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMDHD : ZeDMDBase, IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IFixedSizeDestination, IColorRotationDestination
	{
		public override string Name => "ZeDMD HD";
		public override Dimensions FixedSize { get; } = new Dimensions(256, 64);
		public override bool DmdAllowHdScaling { get; set; } = true;
		public override int Delay { get; set; } = 0;

		private static ZeDMDHD _instance;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMDHD GetInstance(bool debug, int brightness, int rgbOrder, string port)
		{
			if (_instance == null) {
				_instance = new ZeDMDHD { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port };
				_instance.Init();
			}

			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		protected ZeDMDHD()
		{
		}

		protected void Init()
		{
			_pZeDMD = ZeDMD_GetInstance();

			if (!string.IsNullOrEmpty(Port)) {
				ZeDMD.ZeDMD_SetDevice(_pZeDMD, "\\\\.\\" + Port);
			}

			IsAvailable = ZeDMD_Open(_pZeDMD);

			if (!IsAvailable) {
				Logger.Info(Name + " device not found");
				return;
			}
			Logger.Info(Name + " device found");

			if (Debug) { ZeDMD_EnableDebug(_pZeDMD); }
			ZeDMD_SetFrameSize(_pZeDMD, FixedSize.Width, FixedSize.Height);
			if (Brightness >= 0 && Brightness <= 15) { ZeDMD_SetBrightness(_pZeDMD, Brightness); }
			if (RgbOrder >= 0 && RgbOrder <= 5) { ZeDMD_SetRGBOrder(_pZeDMD, RgbOrder); }
		}

		public void RenderGray2(DmdFrame frame)
		{
			DmdAllowHdScaling = true;
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			DmdAllowHdScaling = true;
			SetPalette(frame.Palette);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderGray4(DmdFrame frame)
		{
			DmdAllowHdScaling = true;
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			DmdAllowHdScaling = true;
			SetPalette(frame.Palette);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			DmdAllowHdScaling = true;
			SetPalette(frame.Palette);
			ZeDMD_RenderColoredGray6(_pZeDMD, frame.Data, frame.Rotations);
			_lastFrame = (ColoredFrame)frame.Clone();
		}

		public void RenderRgb24(DmdFrame frame)
		{
			DmdAllowHdScaling = false;
			ZeDMD_RenderRgb24(_pZeDMD, frame.Data);
		}

		public void UpdatePalette(Color[] palette)
		{
			// For Rgb24, we get a new frame for each color rotation.
			// But for ColoredGray6, we have to trigger the frame with
			// an updated palette here.
			if (_lastFrame != null) {
				SetPalette(palette);
				ZeDMD_RenderColoredGray6(_pZeDMD, _lastFrame.Data, _lastFrame.Rotations);
			}
		}
	}
}
