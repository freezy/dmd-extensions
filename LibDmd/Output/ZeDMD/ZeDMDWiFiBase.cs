using System.Runtime.InteropServices;
using System;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public abstract class ZeDMDWiFiBase : ZeDMDBase
	{
		public string WifiAddress { get; set; }
		public int WifiPort { get; set; }
		public string WifiSsid { get; set; }
		public string WifiPassword { get; set; }

		protected new void Init()
		{
			base.Init();

			if (string.IsNullOrEmpty(WifiSsid) && string.IsNullOrEmpty(WifiPassword) && !string.IsNullOrEmpty(WifiAddress) && WifiPort > 0) {

				IsAvailable = ZeDMD_OpenWiFi(_pZeDMD, WifiAddress, WifiPort);
			}
			else {
				OpenUSBConnection();

				if (IsAvailable && !string.IsNullOrEmpty(WifiSsid) && !string.IsNullOrEmpty(WifiPassword) && WifiPort > 0) {
					ZeDMD_EnableDebug(_pZeDMD);
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

			SendConfiguration();
		}

		#region libzedmd

		/// <summary>
		/// WiFi specific libzedmd functions declarations
		/// </summary>

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern bool ZeDMD_OpenWiFi(IntPtr pZeDMD, string ip, int port);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_SetWiFiSSID(IntPtr pZeDMD, string ssid);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_SetWiFiPassword(IntPtr pZeDMD, string password);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_SetWiFiPort(IntPtr pZeDMD, int port);

		#endregion
	}
}
