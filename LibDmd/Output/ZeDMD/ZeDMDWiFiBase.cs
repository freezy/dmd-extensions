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
		protected string WifiAddress { get; set; }
		protected new void Init()
		{
			base.Init();

			if (!string.IsNullOrEmpty(WifiAddress)) {
				IsAvailable = ZeDMD_OpenWiFi(_pZeDMD, WifiAddress);
			} else {
				IsAvailable = ZeDMD_OpenDefaultWiFi(_pZeDMD);
			}

			if (!IsAvailable)
			{
				Logger.Info(Name + " device not found");
				return;
			}
			Logger.Info(Name + " " + Marshal.PtrToStringAnsi(ZeDMD_GetFirmwareVersion(_pZeDMD)) + " device found, UDP delay " + ZeDMD_GetUdpDelay(_pZeDMD) + "ms");
		}

		#region libzedmd

		/// <summary>
		/// WiFi specific libzedmd functions declarations
		/// See https://ppuc.github.io/libzedmd/docs/html/class_ze_d_m_d.html
		/// </summary>

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern bool ZeDMD_OpenDefaultWiFi(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern bool ZeDMD_OpenWiFi(IntPtr pZeDMD, string ip);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern int ZeDMD_GetUdpDelay(IntPtr pZeDMD);


		#endregion
	}
}
