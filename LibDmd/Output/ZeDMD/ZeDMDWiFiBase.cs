﻿using System.Runtime.InteropServices;
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
		protected int WifiPort { get; set; }

		protected new void Init()
		{
			base.Init();

			if (!string.IsNullOrEmpty(WifiAddress) && WifiPort > 0) {
				IsAvailable = ZeDMD_OpenWiFi(_pZeDMD, WifiAddress, WifiPort);
			} else {
				IsAvailable = ZeDMD_OpenDefaultWiFi(_pZeDMD);
			}

			if (!IsAvailable)
			{
				Logger.Info(Name + " device not found");
				return;
			}
			Logger.Info(Name + " device found");
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
		protected static extern bool ZeDMD_OpenWiFi(IntPtr pZeDMD, string ip, int port);

		#endregion
	}
}
