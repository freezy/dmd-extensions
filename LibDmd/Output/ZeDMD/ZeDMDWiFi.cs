using LibDmd.Frame;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMDWiFi : ZeDMDWiFiBase, IRgb24Destination, IRgb565Destination, IMultiSizeDestination
	{
		public override string Name => "ZeDMD WiFi";
		// To leverage ZeDMD's own advanced downscaling we can't use FixedSize and RGB24Stream like ZeDMD HD.
		// By not declaring 192x62 supported, we get a centered 256x64 frame.
		public Dimensions[] Sizes { get; } = { new Dimensions(128, 16), Dimensions.Standard, new Dimensions(256, 64) };

		private static ZeDMDWiFi _instance;
		private Dimensions _currentDimensions = Dimensions.Standard;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMDWiFi GetInstance(bool debug, int brightness, int rgbOrder, string port, string wifiAddress, int wifiPort)
		{
			if (_instance == null)
			{
				_instance = new ZeDMDWiFi { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port, WifiAddress = wifiAddress, WifiPort = wifiPort };
			}

            _instance.Init();
            return _instance;
		}

		private new void Init()
		{
			base.Init();
			ZeDMD_SetFrameSize(_pZeDMD, _currentDimensions.Width, _currentDimensions.Height);
		}

		private void SetDimensions(Dimensions newDim)
		{
			if (_currentDimensions != newDim) {
				_currentDimensions = newDim;
				ZeDMD_SetFrameSize(_pZeDMD, newDim.Width, newDim.Height);
			}
		}

		public void RenderRgb24(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderRgb888(_pZeDMD, frame.Data);
		}

		public void RenderRgb565(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderRgb565(_pZeDMD, frame.Data);
		}
	}
}
