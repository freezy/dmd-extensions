using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	///
	/// DLL documentation can be found here: https://ppuc.github.io/libzedmd/docs/html/class_ze_d_m_d.html
	/// </summary>
	public abstract class ZeDMDBase
	{
		public abstract string Name { get; }
		public bool IsAvailable { get; protected set; }
		public static string DriverVersion => Marshal.PtrToStringAnsi(ZeDMD_GetVersion());
		public bool NeedsDuplicateFrames => false;
		protected bool Debug { get; set; }
		protected int Brightness { get; set; }
		protected int RgbOrder { get; set; }
		protected string Port { get; set; }

		protected IntPtr _pZeDMD = IntPtr.Zero;
		protected readonly Logger Logger = LogManager.GetCurrentClassLogger();
		protected ColoredFrame _lastFrame = null;

		protected void Init() 
		{
			_pZeDMD = ZeDMD_GetInstance();
		}

		protected void SendConfiguration(bool save = false)
		{
			if (Debug) {
				ZeDMD_EnableDebug(_pZeDMD);
			}
			if (Brightness >= 0 && Brightness <= 15) {
				ZeDMD_SetBrightness(_pZeDMD, Brightness);
			}
			if (RgbOrder >= 0 && RgbOrder <= 5) {
				ZeDMD_SetRGBOrder(_pZeDMD, RgbOrder);
			}
			if (save) {
				ZeDMD_SaveSettings(_pZeDMD);
			}
		}

		public void ClearDisplay()
		{
			if (_pZeDMD != IntPtr.Zero) {
				ZeDMD_ClearScreen(_pZeDMD);
			}
		}

		public void Dispose()
		{
			if (IsAvailable == true) {
				if (_pZeDMD != IntPtr.Zero) {
					ZeDMD_Close(_pZeDMD);
				}
				IsAvailable = false;
			}
		}

		public void RenderRgb24(DmdFrame frame)
		{
			ZeDMD_RenderRgb888(_pZeDMD, frame.Data);
		}

		public void RenderRgb565(DmdFrame frame)
		{
			ZeDMD_RenderRgb565(_pZeDMD, frame.Data);
		}


		public void SetPalette(Color[] colors)
		{
			// no palette support here.
		}
		public void SetColor(Color color)
		{
			// no palette support here.
		}


		public void ClearPalette()
		{
		}

		public void ClearColor()
		{
		}

		#region libzedmd

		/// <summary>
		/// libzedmd functions declarations
		/// See https://ppuc.github.io/libzedmd/docs/html/class_ze_d_m_d.html
		/// </summary>

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern IntPtr ZeDMD_GetInstance();

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern bool ZeDMD_SetDevice(IntPtr pZeDMD, string device);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_Close(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_EnableDebug(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_SetBrightness(IntPtr pZeDMD, int brightness);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_SetRGBOrder(IntPtr pZeDMD, int order);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_SaveSettings(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_SetFrameSize(IntPtr pZeDMD, int width, int height);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_ClearScreen(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_RenderRgb888(IntPtr pZeDMD, byte[] frame);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_RenderRgb565(IntPtr pZeDMD, byte[] frame);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern IntPtr  ZeDMD_GetVersion();

		#endregion

	}
}
