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
		public static ZeDMDHD GetInstance(bool debug, int brightness, int rgbOrder, string port, bool scaleRgb24)
		{
			if (_instance == null) {
				_instance = new ZeDMDHD { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port, ScaleRgb24 = scaleRgb24 };
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

		protected new void Init()
		{
			base.Init();
			OpenUSBConnection();
			SendConfiguration();
			ZeDMD_SetFrameSize(_pZeDMD, FixedSize.Width, FixedSize.Height);
		}

		public void RenderGray2(DmdFrame frame)
		{
			DmdAllowHdScaling = true;
			ZeDMD_EnablePreUpscaling(_pZeDMD);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			DmdAllowHdScaling = true;
			ZeDMD_EnablePreUpscaling(_pZeDMD);
			SetPalette(frame.Palette);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderGray4(DmdFrame frame)
		{
			DmdAllowHdScaling = true;
			ZeDMD_DisablePreUpscaling(_pZeDMD);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			DmdAllowHdScaling = true;
			ZeDMD_DisablePreUpscaling(_pZeDMD);
			SetPalette(frame.Palette);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			DmdAllowHdScaling = true;
			ZeDMD_EnablePreUpscaling(_pZeDMD);
			SetPalette(frame.Palette);
			ZeDMD_RenderColoredGray6(_pZeDMD, frame.Data, frame.Rotations);
			_lastFrame = (ColoredFrame)frame.Clone();
		}

		public void RenderRgb24(DmdFrame frame)
		{
			DmdAllowHdScaling = ScaleRgb24;
			ZeDMD_EnablePreUpscaling(_pZeDMD);
			ZeDMD_RenderRgb24(_pZeDMD, frame.Data);
		}
	}
}
