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
		public new string Name => "ZeDMD HD WiFi";

		public new Dimensions FixedSize { get; } = new Dimensions(256, 64);
		public new bool DmdAllowHdScaling { get; } = true;

		private static ZeDMDHDWiFi _instance;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public new static ZeDMDWiFi GetInstance(bool debug, int brightness, int rgbOrder, string port, string wifiAddress, int wifiPort, string wifiSsid, string wifiPassword)
		{
			if (_instance == null) {
				_instance = new ZeDMDHDWiFi { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port, WifiAddress = wifiAddress, WifiPort = wifiPort, WifiSsid = wifiSsid, WifiPassword = wifiPassword };
				_instance.Init();
			}

			return _instance;
		}

	}
}
