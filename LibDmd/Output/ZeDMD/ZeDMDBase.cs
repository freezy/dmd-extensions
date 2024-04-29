using System;
using System.Runtime.InteropServices;
using LibDmd.Common;
using System.Windows.Media;
using NLog;
using System.Linq;

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
		public bool NeedsDuplicateFrames => false;
		protected bool Debug { get; set; }
		protected int Brightness { get; set; }
		protected int RgbOrder { get; set; }
		protected string Port { get; set; }
		protected bool ScaleRgb24 { get; set; }

		protected IntPtr _pZeDMD = IntPtr.Zero;
		protected readonly Logger Logger = LogManager.GetCurrentClassLogger();
		protected ColoredFrame _lastFrame = null;

		protected void Init()
		{
			_pZeDMD = ZeDMD_GetInstance();
		}

		protected void OpenUSBConnection()
		{
			if (!string.IsNullOrEmpty(Port)) {
				ZeDMD_SetDevice(_pZeDMD, @"\\.\" + Port);
			}

			IsAvailable = ZeDMD_Open(_pZeDMD);

			if (!IsAvailable) {
				Logger.Info(Name + " device not found");
				return;
			}
			Logger.Info(Name + " device found");
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
			ClearDisplay();
		}

		public void SetPalette(Color[] colors)
		{
			if (_pZeDMD == IntPtr.Zero) {
				return;
			}

			byte[] paletteBuffer = Enumerable.Repeat((byte)0x0, 64 * 3).ToArray();
			var numOfColors = colors.Length;

			if (numOfColors != 4 && numOfColors != 16 && numOfColors != 64) {
				return;
			}

			// Custom palettes could be defined with less colors as required by the ROM.
			// So we interpolate bigger palettes up to 64 colors.
			while (numOfColors <= 64) {
				var palette = ColorUtil.GetPalette(colors, numOfColors);
				var pos = 0;

				foreach (var color in palette) {
					paletteBuffer[pos++] = color.R;
					paletteBuffer[pos++] = color.G;
					paletteBuffer[pos++] = color.B;
				}
				ZeDMD_SetPalette(_pZeDMD, paletteBuffer, numOfColors);

				numOfColors *= 4;
			}
		}

		public void UpdatePalette(Color[] palette)
		{
			// For Rgb24, we get a new frame for each color rotation.
			// But for ColoredGray6, we have to trigger the frame with
			// an updated palette here.
			if (_lastFrame != null) {
				SetPalette(palette);
				ZeDMD_RenderColoredGray6(_pZeDMD, _lastFrame.Data, null);// _lastFrame.Rotations);
			}
		}

		public void SetColor(Color color)
		{
			SetPalette(ColorUtil.GetPalette(new[] { Colors.Black, color }, 4));
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
		protected static extern bool ZeDMD_Open(IntPtr pZeDMD);

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
		protected static extern void ZeDMD_EnablePreDownscaling(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_DisablePreDownscaling(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_EnablePreUpscaling(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_DisablePreUpscaling(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_EnableUpscaling(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_DisableUpscaling(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_EnforceStreaming(IntPtr pZeDMD);

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
		protected static extern void ZeDMD_SetPalette(IntPtr pZeDMD, byte[] palette, int numColors);

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
		protected static extern void ZeDMD_RenderGray2(IntPtr pZeDMD, byte[] frame);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_RenderGray4(IntPtr pZeDMD, byte[] frame);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_RenderColoredGray6(IntPtr pZeDMD, byte[] frame, byte[] rotations);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		protected static extern void ZeDMD_RenderRgb24(IntPtr pZeDMD, byte[] frame);

		#endregion

	}
}
