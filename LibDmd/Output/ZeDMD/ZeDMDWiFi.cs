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
	public class ZeDMDWiFi : ZeDMDBase, IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IFixedSizeDestination, IColorRotationDestination
	{
		public string Name => "ZeDMD WiFi";
		public string WifiAddress { get; set; }
		public int WifiPort { get; set; }
		public string WifiSsid { get; set; }
		public string WifiPassword { get; set; }

		public Dimensions FixedSize { get; } = Dimensions.Standard;
		public bool DmdAllowHdScaling { get; } = false;

		private static ZeDMDWiFi _instance;

		protected ColoredFrame _lastFrame = null;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMDWiFi GetInstance(bool debug, int brightness, int rgbOrder, string wifiAddress, int wifiPort, string wifiSsid, string wifiPassword)
		{
			if (_instance == null)
			{
				_instance = new ZeDMDWiFi { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, WifiAddress = wifiAddress, WifiPort = wifiPort, WifiSsid = wifiSsid, WifiPassword = wifiPassword };
				_instance.Init();
			}
	
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		protected ZeDMDWiFi()
		{
		}

		protected void Init()
		{
			_pZeDMD = ZeDMD_GetInstance();

			if (!string.IsNullOrEmpty(WifiAddress) && WifiPort > 0) {
				IsAvailable = ZeDMD_OpenWiFi(_pZeDMD, WifiAddress, WifiPort);
			}
			else {
				IsAvailable = ZeDMD_Open(_pZeDMD);

				if (IsAvailable && !string.IsNullOrEmpty(WifiSsid) && !string.IsNullOrEmpty(WifiPassword) && WifiPort > 0) {
					//ZeDMD_EnableDebug(_pZeDMD);
					ZeDMD_SetWiFiSSID(_pZeDMD, WifiSsid);
					ZeDMD_SetWiFiPassword(_pZeDMD, WifiPassword);
					ZeDMD_SetWiFiPort(_pZeDMD, WifiPort);
					ZeDMD_SaveSettings(_pZeDMD);
					Logger.Info(Name + " WiFi credentials submitted");
					ZeDMD_Close(_pZeDMD);
					IsAvailable = false;
					return;
				}
			}

			if (!IsAvailable)
			{
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
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			SetPalette(frame.Palette);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderGray4(DmdFrame frame)
		{
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			SetPalette(frame.Palette);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			_lastFrame = (ColoredFrame)frame.Clone();
			SetPalette(frame.Palette);
			ZeDMD_RenderColoredGray6(_pZeDMD, frame.Data, frame.Rotations);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			ZeDMD_RenderRgb24(_pZeDMD, frame.Data);
		}

		public void UpdatePalette(Color[] palette)
		{
			// For Rgb24, we get a new frame for each color rotation.
			// But for ColoredGray6, we hae to trigger the frame with
			// an updated palette here.
			if (_lastFrame != null) {
				SetPalette(palette);
				ZeDMD_RenderColoredGray6(_pZeDMD, _lastFrame.Data, _lastFrame.Rotations);
			}
		}
	}
}
