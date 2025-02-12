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

		protected IntPtr _pZeDMD = IntPtr.Zero;
		protected readonly Logger Logger = LogManager.GetCurrentClassLogger();
		protected ColoredFrame _lastFrame = null;
		private GCHandle handle;

		protected void LogHandler(string format, IntPtr args, IntPtr pUserData)
		{
#if PLATFORM_X64
			Logger.Debug("Trying to convert libzedmd log message: " + format);
			Logger.Info("libzedmd: " + Marshal.PtrToStringAnsi(ZeDMD_FormatLogMessage(format, args, pUserData)));
#endif
		}

		protected void Init()
		{
			_pZeDMD = ZeDMD_GetInstance();
			Logger.Info("Using libzedmd version " + DriverVersion);

#if PLATFORM_X64
			ZeDMD_LogCallback callbackDelegate = new ZeDMD_LogCallback(LogHandler);
			// Keep a reference to the delegate to prevent GC from collecting it
			handle = GCHandle.Alloc(callbackDelegate);
			ZeDMD_SetLogCallback(_pZeDMD, callbackDelegate, IntPtr.Zero);
#else
			Logger.Warn("Forwarding libzedmd and libserialport logging is not working on x86.");
#endif
		}

		protected void SendConfiguration()
		{
			if (Debug) {
				ZeDMD_EnableDebug(_pZeDMD);
			}
			if (Brightness >= 0 && Brightness <= 15) {
				ZeDMD_SetBrightness(_pZeDMD, Brightness);
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
					ZeDMD_ClearScreen(_pZeDMD);
					ZeDMD_Close(_pZeDMD);
				}
				IsAvailable = false;
			}

			if (handle.IsAllocated) {
				handle.Free();
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
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		protected delegate void ZeDMD_LogCallback(string format, IntPtr args, IntPtr pUserData);

		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		protected static extern void ZeDMD_SetLogCallback(IntPtr pZeDMD, ZeDMD_LogCallback callback, IntPtr pUserData);

		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		protected static extern IntPtr ZeDMD_FormatLogMessage(string format, IntPtr args, IntPtr pUserData);
#endif

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
		protected static extern IntPtr ZeDMD_GetFirmwareVersion(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern IntPtr ZeDMD_GetVersion();

		#endregion

	}
}
