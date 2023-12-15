using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMD : ZeDMDBase, IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IMultiSizeDestination
	{
		public string Name => "ZeDMD";

		public Dimensions[] Sizes { get; } = { new Dimensions(128, 16), Dimensions.Standard, new Dimensions(192, 64), new Dimensions(256, 64) };
		
		private static ZeDMD _instance;
		protected Dimensions _currentDimensions = Dimensions.Standard;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMD GetInstance(bool debug, int brightness, int rgbOrder, string port)
		{
			if (_instance == null)
			{
				_instance = new ZeDMD { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port };
				_instance.Init();
			}
	
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		protected ZeDMD()
		{
		}

		protected void Init()
		{
			_pZeDMD = ZeDMD_GetInstance();

			if (!string.IsNullOrEmpty(Port)) {
				ZeDMD.ZeDMD_SetDevice(_pZeDMD, "\\\\.\\" + Port);
			}

			IsAvailable = ZeDMD_Open(_pZeDMD);

			if (!IsAvailable)
			{
				Logger.Info(Name + " device not found");
				return;
			}
			Logger.Info(Name + " device found");

			if (Debug) { ZeDMD_EnableDebug(_pZeDMD); }
			ZeDMD_SetFrameSize(_pZeDMD, _currentDimensions.Width, _currentDimensions.Height);
			if (Brightness >= 0 && Brightness <= 15) { ZeDMD_SetBrightness(_pZeDMD, Brightness); }
			if (RgbOrder >= 0 && RgbOrder <= 5) { ZeDMD_SetRGBOrder(_pZeDMD, RgbOrder); }
		}

		public void SetDimensions(Dimensions newDim)
		{
			if (_currentDimensions != newDim) {
				_currentDimensions = newDim;
				ZeDMD_SetFrameSize(_pZeDMD, newDim.Width, newDim.Height);
			}
		}

		public void RenderGray2(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderGray4(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette);
			ZeDMD_RenderColoredGray6(_pZeDMD, frame.Data, frame.Rotations);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderRgb24(_pZeDMD, frame.Data);
		}
	}
}
