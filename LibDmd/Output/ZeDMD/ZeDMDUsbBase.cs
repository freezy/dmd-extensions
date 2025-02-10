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
	public abstract class ZeDMDUsbBase : ZeDMDBase
	{
		protected string Port { get; set; }

		protected new void Init()
		{
			base.Init();

			if (!string.IsNullOrEmpty(Port)) {
				ZeDMD_SetDevice(_pZeDMD, Port);
			}

			IsAvailable = ZeDMD_Open(_pZeDMD);

			if (!IsAvailable) {
				if (string.IsNullOrEmpty(Port)) {
					Logger.Info(Name + " device not found at any port");
				}
				else {
					Logger.Info(Name + " device not found at port " + Port);
				}
				return;
			}

			if (string.IsNullOrEmpty(Port)) {
				Logger.Info(Name + " " + Marshal.PtrToStringAnsi(ZeDMD_GetFirmwareVersion(_pZeDMD)) + " device found, USB package size " + ZeDMD_GetUsbPackageSize(_pZeDMD));
			} else {
				Logger.Info(Name + " " + Marshal.PtrToStringAnsi(ZeDMD_GetFirmwareVersion(_pZeDMD)) + " device found at port " + Port + ", USB package size " + ZeDMD_GetUsbPackageSize(_pZeDMD));
			}
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
		protected static extern bool ZeDMD_Open(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern int ZeDMD_GetUsbPackageSize(IntPtr pZeDMD);

		#endregion
	}
}
