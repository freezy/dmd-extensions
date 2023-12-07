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
	/// </summary>
	public class ZeDMD : IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IRawOutput, IMultiSizeDestination
	{
		public string Name => "ZeDMD";
		public bool IsAvailable { get; private set; }
		public bool NeedsDuplicateFrames => false;
		// libzedmd has it's own queuing.
		public int Delay { get; set; } = 0;
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
		public static ZeDMD GetInstance()
		{
			if (_instance == null)
			{
				_instance = new ZeDMD();
			}

			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private ZeDMD()
		{
			_pZeDMD = ZeDMD_GetInstance();
			IsAvailable = ZeDMD_Open(_pZeDMD);

			if (!IsAvailable)
			{
				Logger.Info(Name + " device not found");
				return;
			}
			Logger.Info(Name + " device found");

			// Different modes require different palette sizes. This one should be safe for all.
			_paletteBuffer = new byte[64 * 3];

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
			ZeDMD_SetDefaultPalette(_pZeDMD, 2);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette, frame.NumColors);
			ZeDMD_RenderGray2(_pZeDMD, frame.Data);
		}

		public void RenderGray4(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_SetDefaultPalette(_pZeDMD, 4);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette, frame.NumColors);
			ZeDMD_RenderGray4(_pZeDMD, frame.Data);
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			SetDimensions(frame.Dimensions);
			SetPalette(frame.Palette, frame.NumColors);
			ZeDMD_RenderColoredGray6(_pZeDMD, frame.Data, frame.Rotations);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			SetDimensions(frame.Dimensions);
			ZeDMD_RenderRgb24(_pZeDMD, frame.Data);
		}

		public void RenderRaw(byte[] data)
		{
			// Todo
		}

		public void ClearDisplay()
		{
			ZeDMD_ClearScreen(_pZeDMD);
		}

		public void Dispose()
		{
			ZeDMD_Close(_pZeDMD);
		}

		private void SetPalette(Color[] palette, int ncol)
		{
			var paletteChanged = false;
			for (var i = 0; i < ncol; i++) {
				var color = palette[i];
				var j = i * 3;
				paletteChanged = paletteChanged || (_paletteBuffer[j] != color.R || _paletteBuffer[j + 1] != color.G || _paletteBuffer[j + 2] != color.B);
				_paletteBuffer[j] = color.R;
				_paletteBuffer[j + 1] = color.G;
				_paletteBuffer[j + 2] = color.B;
			}

			if (paletteChanged) {
				ZeDMD_SetPalette(_pZeDMD, _paletteBuffer, ncol);
			}
		}

		public void SetColor(Color color)
		{
		}

		public void SetPalette(Color[] colors)
		{
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
		private static extern void ZeDMD_Close(IntPtr pZeDMD);

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
		private static extern void ZeDMD_SetDefaultPalette(IntPtr pZeDMD, int bitDepth);

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
