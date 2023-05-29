using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Output.PinUp;
using NLog;

namespace LibDmd.Converter.Serum
{
	public class Serum : AbstractSource, IConverter, IColoredGray6Source
	{
		public override string Name => "Serum";
		public IEnumerable<FrameFormat> From { get; } = new [] { FrameFormat.Gray2, FrameFormat.Gray4 };
		public bool IsLoaded;
		private uint NumTriggersAvailable { get; }

		/// <summary>
		/// Number of colours in the manufacturer's ROM
		/// </summary>
		public readonly uint NumColors;

		// cROM components
		/// <summary>
		/// Frame dimensions in LEDs
		/// </summary>
		private readonly Dimensions _dimensions;


		/// <summary>
		/// =active instance of Pinup Player if available, =null if not
		/// </summary>
		private PinUpOutput _activePupOutput;
		
		private readonly Color[] _colorPalette = new Color[64];
		private readonly byte[] _bytePalette = new byte[64 * 3];
		private readonly DmdFrame _frame;
		private readonly byte[][] _planes;
		private readonly byte[] _rotations;
		
		private readonly Subject<ColoredFrame> _coloredGray6AnimationFrames = new Subject<ColoredFrame>();

		public ScalerMode ScalerMode { get; set; }

		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


		/// <summary>
		/// Maximum amount of color rotations per frame
		/// </summary>
		private const int MAX_COLOR_ROTATIONS = 8;

		public Serum(string altcolorPath, string romName)
		{
			uint numTriggers = 0;
			
#if PLATFORM_X64
			const string dllName = "serum64.dll";
#else
			const string dllName = "serum.dll";
#endif
			
			if (File.Exists(dllName)) {
				Logger.Info($"[serum] Found {dllName} at {Directory.GetCurrentDirectory()}.");
			}

			var width = 0;
			var height = 0;
			if (!Serum_Load(altcolorPath, romName, ref width, ref height, ref NumColors, ref numTriggers)) {
				IsLoaded = false;
				return;
			}

			_dimensions = new Dimensions(width, height);
			NumTriggersAvailable = numTriggers;
			IsLoaded = true;
			_frame = new DmdFrame(_dimensions, ((int)NumColors).GetBitLength());
			
			_planes = new byte[6][];
			for (uint ti = 0; ti < 6; ti++) {
				_planes[ti] = new byte[_dimensions.Surface / 8];
			}
			_rotations = new byte[MAX_COLOR_ROTATIONS * 3];
		}
		
		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6AnimationFrames;
		
		public void SetPinupInstance(PinUpOutput puo)
		{
			_activePupOutput = puo;
			if ((puo != null) && (NumTriggersAvailable > 0)) {
				puo.PuPFrameMatching = false;
			}
		}

		public void Dispose()
		{
			Serum_Dispose();
			_activePupOutput = null;
			IsLoaded = false;
		}

		public void Init()
		{
		}

		public void Convert(DmdFrame frame)
		{
			Buffer.BlockCopy(frame.Data, 0, _frame.Data, 0, frame.Data.Length);
			
			uint triggerId = 0xFFFFFFFF;
			if ((_activePupOutput != null) && ((_activePupOutput.IsPuPTrigger == false) || (_activePupOutput.PuPFrameMatching == true)))
			{
				if (NumColors == 16)
					_activePupOutput.RenderGray4(_frame);
				else
					_activePupOutput.RenderGray2(_frame);
			}

			Serum_Colorize(_frame.Data, _dimensions.Width, _dimensions.Height, _bytePalette, _rotations, ref triggerId);

			for (uint ti = 0; ti < MAX_COLOR_ROTATIONS; ti++) {
				if ((_rotations[ti * 3] >= 64) || (_rotations[ti * 3] + _rotations[ti * 3 + 1] > 64)) {
					_rotations[ti * 3] = 255;
				}
			}
			
			if (_activePupOutput != null && triggerId != 0xFFFFFFFF) {
				_activePupOutput.SendTriggerId((ushort)triggerId);
			}

			// convert to planes
			FrameUtil.Split(frame.Dimensions, _planes.Length, _frame.Data, _planes);

			// send the colored frame
			_coloredGray6AnimationFrames.OnNext(new ColoredFrame(_dimensions, _planes, ConvertPalette(), _rotations));
		}
		
		public static string GetVersion()
		{
			IntPtr pointer = Serum_GetMinorVersion();
			string str = Marshal.PtrToStringAnsi(pointer);
			return str;
		}

		private byte[][] ConvertToPlanes(byte colorBitDepth)
		{
			byte bitMask = 1;
			var tj = 0;
			for (var tk = 0; tk < colorBitDepth; tk++) {
				_planes[tk][tj] = 0;
			}

			var len = _dimensions.Surface;
			for (var ti = 0; ti < len; ti++) {
				byte tl = 1;
				for (var tk = 0; tk < colorBitDepth; tk++) {
					if ((_frame.Data[ti] & tl) > 0) {
						_planes[tk][tj] |= bitMask;
					}
					tl <<= 1;
				}
				if (bitMask == 0x80) {
					bitMask = 1;
					tj++;
					if (tj < len / 8) {
						for (var tk = 0; tk < colorBitDepth; tk++) {
							_planes[tk][tj] = 0;
						}
					}
				} else {
					bitMask <<= 1;
				}
			}

			return _planes;
		}

		private Color[] ConvertPalette()
		{
			for (int ti = 0; ti < 64; ti++) {
				_colorPalette[ti].A = 255;
				_colorPalette[ti].R = _bytePalette[ti * 3];
				_colorPalette[ti].G = _bytePalette[ti * 3 + 1];
				_colorPalette[ti].B = _bytePalette[ti * 3 + 2];
			}

			return _colorPalette;
		}
		
		#region Serum API

		/// <summary>
		/// Serum library functions declarations
		/// </summary>

		/// <summary>
		/// Serum_Load: Function to call at table opening time
		/// </summary>
		/// <param name="altcolorpath">path of the altcolor directory, e.g. "c:/Visual Pinball/VPinMame/altcolor/"</param>
		/// <param name="romname">rom name</param>
		/// <param name="width">out: colorized rom width in LEDs</param>
		/// <param name="height">out: colorized rom height in LEDs</param>
		/// <param name="numColors">out: number of colours in the manufacturer rom</param>
		/// <returns></returns>
#if PLATFORM_X64
		[DllImport("serum64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		// C format: bool Serum_Load(const char* const altcolorpath, const char* const romname, int* pwidth, int* pheight, unsigned int* pnocolors, unsigned int* pntriggers)
		private static extern bool Serum_Load(string altcolorpath, string romname, ref int width, ref int height, ref uint numColors, ref uint triggernb);
		
		/// <summary>
		/// Serum_Colorize: Function to call with a VpinMame frame to colorize it
		/// </summary>
		/// <param name="frame">width*height bytes: in: buffer with the VPinMame frame out: buffer with the colorized frame</param>
		/// <param name="width">frame width in LEDs</param>
		/// <param name="height">frame height in LEDs</param>
		/// <param name="palette">64*3 bytes: out: RGB palette description 64 colours with their R, G and B component</param>
		/// <param name="rotations">8*3 bytes: out: colour rotations 8 maximum per frame with first colour, number of colour and time interval in 10ms</param>
#if PLATFORM_X64
		[DllImport("serum64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		// C format: void Serum_Colorize(UINT8* frame, int width, int height, UINT8* palette, UINT8* rotations, UINT32* triggerID)
		private static extern void Serum_Colorize(Byte[] frame, int width, int height, byte[] palette, byte[] rotations,ref uint triggerID);
		
		/// <summary>
		/// Serum_Dispose: Function to call at table unload time to free allocated memory
		/// </summary>
#if PLATFORM_X64 
		[DllImport("serum64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		// C format: void Serum_Dispose(void)
		private static extern void Serum_Dispose();

		/// <summary>
		/// Serum_GetMinorVersion: Function to get dll version
		/// </summary>
#if PLATFORM_X64 
		[DllImport("serum64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		// C format: const char* Serum_GetMinorVersion();
		private static extern IntPtr Serum_GetMinorVersion();

		#endregion
	}
}
