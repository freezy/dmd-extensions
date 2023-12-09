using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMD : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IMultiSizeDestination
	{
		public string Name => "ZeDMD";
		public bool IsAvailable { get; private set; }
		public bool NeedsDuplicateFrames => false;
		// libzedmd has it's own queuing.
		public int Delay { get; set; } = 0;
		public bool Debug { get; set; }
		public int Brightness { get; set; }
		public int RgbOrder { get; set; }
		public string WifiAddress { get; set; }
		public int WifiPort { get; set; }
		public string WifiSsid { get; set; }
		public string WifiPassword { get; set; }

		public Dimensions[] Sizes { get; } = { new Dimensions(128, 16), Dimensions.Standard, new Dimensions(192, 64), new Dimensions(256, 64) };
		
		private static ZeDMD _instance;
		private static IntPtr _pZeDMD;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private Dimensions _currentDimensions = Dimensions.Standard;
		private byte[] _paletteBuffer;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMD GetInstance(bool debug, int brightness, int rgbOrder, string wifiAddress, int wifiPort, string wifiSsid, string wifiPassword)
		{
			if (_instance == null)
			{
				_instance = new ZeDMD { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, WifiAddress = wifiAddress, WifiPort = wifiPort, WifiSsid = wifiSsid, WifiPassword = wifiPassword };
				_instance.Init();
			}
	
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private ZeDMD()
		{
			_pZeDMD = ZeDMD_GetInstance();
		}

		private void Init()
		{
			if (!string.IsNullOrEmpty(WifiAddress) && WifiPort > 0) {
				IsAvailable = ZeDMD_OpenWiFi(_pZeDMD, WifiAddress, WifiPort);
			}
			else {
				IsAvailable = ZeDMD_Open(_pZeDMD);

				if (IsAvailable && !string.IsNullOrEmpty(WifiSsid) && !string.IsNullOrEmpty(WifiPassword)) {
					ZeDMD_SetWiFiSSID(_pZeDMD, WifiSsid);
					ZeDMD_SetWiFiPassword(_pZeDMD, WifiPassword);
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

			// Different modes require different palette sizes. This one should be safe for all.
			_paletteBuffer = new byte[64 * 3];

			if (Debug) { ZeDMD_EnableDebug(_pZeDMD); }
			if (Brightness >= 0 && Brightness <= 15) { ZeDMD_SetBrightness(_pZeDMD, Brightness); }
			if (RgbOrder >= 0 && RgbOrder <= 15) { ZeDMD_SetRGBOrder(_pZeDMD, RgbOrder); }

			ZeDMD_SetFrameSize(_pZeDMD, _currentDimensions.Width, _currentDimensions.Height);
		}

		public void SetDimensions(Dimensions newDim)
		{
			if (_currentDimensions != newDim) {
				_currentDimensions = newDim;
				ZeDMD_SetFrameSize(_pZeDMD, newDim.Width, newDim.Height);
			}
		}

		public void RenderGray2(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderGray4(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette);
			ZeDMD_RenderColoredGray6(_pZeDMD, frame.Data, frame.Rotations);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderRgb24(_pZeDMD, frame.Data);
		}

		public void ClearDisplay()
		{
			qZeDMD_ClearScreen(_pZeDMD);
		}

		public void Dispose()
		{
			ZeDMD_ClearScreen(_pZeDMD);
		}

		public void SetPalette(Color[] palette)
		{
			var paletteChanged = false;
			for (var i = 0; i < palette.Length; i++) {
				var color = palette[i];
				var j = i * 3;
				paletteChanged = paletteChanged || (_paletteBuffer[j] != color.R || _paletteBuffer[j + 1] != color.G || _paletteBuffer[j + 2] != color.B);
				_paletteBuffer[j] = color.R;
				_paletteBuffer[j + 1] = color.G;
				_paletteBuffer[j + 2] = color.B;
			}

			if (paletteChanged) {
				ZeDMD_SetPalette(_pZeDMD, _paletteBuffer, palette.Length);
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
		/// </summary>

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern IntPtr ZeDMD_GetInstance();

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern bool ZeDMD_Open(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern bool ZeDMD_OpenWiFi(IntPtr pZeDMD, string ip, int port);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_Close(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_EnableDebug(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_SetBrightness(IntPtr pZeDMD, int brightness);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_SetRGBOrder(IntPtr pZeDMD, int order);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_SetWiFiSSID(IntPtr pZeDMD, string ssid);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_SetWiFiPassword(IntPtr pZeDMD, string password);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_SaveSettings(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_SetFrameSize(IntPtr pZeDMD, int width, int height);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_SetPalette(IntPtr pZeDMD, byte[] palette, int numColors);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_ClearScreen(IntPtr pZeDMD);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_RenderGray2(IntPtr pZeDMD, byte[] frame);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_RenderGray4(IntPtr pZeDMD, byte[] frame);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_RenderColoredGray6(IntPtr pZeDMD, byte[] frame, byte[] rotations);

#if PLATFORM_X64
		[DllImport("zedmd64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("zedmd.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		private static extern void ZeDMD_RenderRgb24(IntPtr pZeDMD, byte[] frame);

		#endregion

	}
}
