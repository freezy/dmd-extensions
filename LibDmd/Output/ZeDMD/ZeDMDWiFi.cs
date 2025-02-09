using LibDmd.Frame;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMDWiFi : ZeDMDWiFiBase, IRgb24Destination, IRgb565Destination, IFixedSizeDestination
	{
		public override string Name => "ZeDMD WiFi";
		public Dimensions FixedSize { get; } = new Dimensions(128, 32);
		public virtual bool DmdAllowHdScaling { get; } = false;

		private static ZeDMDWiFi _instance;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMDWiFi GetInstance(bool debug, int brightness, string wifiAddress)
		{
			if (_instance == null)
			{
				_instance = new ZeDMDWiFi { Debug = debug, Brightness = brightness, WifiAddress = wifiAddress };
			}

            _instance.Init();
            return _instance;
		}

		private new void Init()
		{
			base.Init();
			if (IsAvailable) {
				SendConfiguration();
				ZeDMD_SetFrameSize(_pZeDMD, FixedSize.Width, FixedSize.Height);
				ClearDisplay();
			}
		}
	}
}
