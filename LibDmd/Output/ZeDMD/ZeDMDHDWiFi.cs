using LibDmd.Frame;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMDHDWiFi : ZeDMDWiFiBase, IRgb24Destination, IRgb565Destination, IFixedSizeDestination
	{
		public override string Name => "ZeDMD HD WiFi";
		public virtual Dimensions FixedSize { get; } = new Dimensions(256, 64);
		public virtual bool DmdAllowHdScaling { get; protected set; } = true;

		private static ZeDMDHDWiFi _instance;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMDHDWiFi GetInstance(bool debug, int brightness, int rgbOrder, string port, bool scaleRgb24, string wifiAddress, int wifiPort)
		{
			if (_instance == null) {
				_instance = new ZeDMDHDWiFi { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port, ScaleRgb24 = scaleRgb24, WifiAddress = wifiAddress, WifiPort = wifiPort };
			}

            _instance.Init();
            return _instance;
		}

		private new void Init()
		{
			base.Init();
			ZeDMD_SetFrameSize(_pZeDMD, FixedSize.Width, FixedSize.Height);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			DmdAllowHdScaling = ScaleRgb24;
			ZeDMD_RenderRgb888(_pZeDMD, frame.Data);
		}

		public void RenderRgb565(DmdFrame frame)
		{
			ZeDMD_RenderRgb565(_pZeDMD, frame.Data);
		}
	}
}
