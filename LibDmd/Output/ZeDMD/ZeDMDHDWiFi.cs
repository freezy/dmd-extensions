using LibDmd.Frame;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMDHDWiFi : ZeDMDWiFi
	{
		public override string Name => "ZeDMD HD WiFi";

		public override Dimensions FixedSize { get; } = new Dimensions(256, 64);
		public override bool DmdAllowHdScaling { get; set; } = true;
		// libzedmd has it's own queuing.
		public override int Delay { get; set; } = 0;

		private static ZeDMDHDWiFi _instance;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMDHDWiFi GetInstance(bool debug, int brightness, int rgbOrder, string port, bool scaleRgb24, string wifiAddress, int wifiPort, string wifiSsid, string wifiPassword)
		{
			if (_instance == null) {
				_instance = new ZeDMDHDWiFi { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port, ScaleRgb24 = scaleRgb24, WifiAddress = wifiAddress, WifiPort = wifiPort, WifiSsid = wifiSsid, WifiPassword = wifiPassword };
				_instance.Init();
			}

			return _instance;
		}

	}
}
