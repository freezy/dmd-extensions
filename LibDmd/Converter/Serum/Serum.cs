using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Common.HeatShrink;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Output;
using Newtonsoft.Json.Linq;
using NLog;
using SharpGL;

namespace LibDmd.Converter.Serum
{
	enum Serum_Version // returned by Serum_Load in *SerumVersion
	{
		SERUM_V1,
		SERUM_V2
	};

	/// <summary>
	/// Structure to manage former format Serum files.
	/// All the elements must have been allocated BEFORE the structure is used in colorization and color rotation functions
	/// </summary>
	/*[StructLayout(LayoutKind.Sequential)]
	public struct Serum_Frame
	{
		public IntPtr frame; // Pointer to the destination colorized frame (128*32 or 196*64 bytes)
		public IntPtr palette; // Pointer to the destination palette (64*3 bytes)
		public IntPtr rotations; // Pointer to the destination color rotations (8*3 bytes)
		public IntPtr triggerID; // Pointer to an UINT to get the PuP trigger returned by the frame
		public IntPtr frameID; // Pointer to an UINT to get the identified frame in the Serum file (mainly for the editor)
	}
	/// <summary>
	/// Structure to manage new format Serum files.
	/// All the elements must have been allocated BEFORE the structure is used in colorization and color rotation functions
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct Serum_Frame_New
	{
		public IntPtr frame32; // Pointer to the destination colorized RGB565 frame in P32 (128*32 or 96*32 UINT16)
		public IntPtr width32; // Pointer to an UINT to get the width of the P32 frame (128 or 96)
		public IntPtr rotations32; // Pointer to the destination colors rotations of the P32 frame (4*64 UINT16)
		public IntPtr rotationsinframe32; // Pointer to the precalculated color rotations of the P32 frame (width32*32*2 UINT16):
										  // for each pixel, first UINT16=number of the detected rotation for this pixel (0xffff if not in a rotation), second UINT16=position in the rotation
		public IntPtr frame64; // Pointer to the destination colorized RGB565 frame in P64 (256*64 or 192*64 UINT16)
		public IntPtr width64; // Pointer to an UINT to get the width of the P64 frame (256 or 192)
		public IntPtr rotations64;  // Pointer to the destination colors rotations of the P64 frame (4*64 UINT16)
		public IntPtr rotationsinframe64; // Pointer to the precalculated color rotations of the P64 frame (width64*64*2 UINT16):
										  // for each pixel, first UINT16=number of the detected rotation for this pixel (0xffff if not in a rotation), second UINT16=position in the rotation
		public IntPtr triggerID; // Pointer to an UINT to get the PuP trigger returned by the frame
		public IntPtr flags; // return flags:
					  // if flags & 1 : frame32 has been filled
					  // if flags & 2 : frame64 has been filled
					  // if none of them, display the original frame
		public IntPtr frameID; // Pointer to an UINT to get the identified frame in the Serum file (for the editor and for debugging)
	}*/
	[StructLayout(LayoutKind.Sequential)]
	public struct Serum_Frame_Struc
	{
		// data for v1 Serum format
		public IntPtr frame; // return the colorized frame
		public IntPtr palette; // and its palette
		public IntPtr rotations; // and its color rotations
						  // data for v2 Serum format
						  // the frame (frame32 or frame64) corresponding to the resolution of the ROM must ALWAYS be defined
						  // if a frame pointer is defined, its width, rotations and rotationsinframe pointers must be defined
		public IntPtr frame32;
		public uint width32; // 0 is returned if the 32p colorized frame is not available for this frame
		public IntPtr rotations32;
		public IntPtr rotationsinframe32; // [width32*32*2] precalculated array to tell if a color is in a color rotations of the frame ([X*Y*0]=0xffff if not part of a rotation)
		public IntPtr modifiedelements32; // (optional) 32P pixels modified during the last rotation
		public IntPtr frame64;
		public uint width64; // 0 is returned if the 64p colorized frame is not available for this frame
		public IntPtr rotations64;
		public IntPtr rotationsinframe64;  // [width64*64*2] precalculated array to tell if a color is in a color rotations of the frame ([X*Y*0]=0xffff if not part of a rotation)
		public IntPtr modifiedelements64; // (optional) 64P pixels modified during the last rotation
										  // common data
		public uint SerumVersion; // SERUM_V1 or SERUM_V2
		/// <summary>
		/// flags for return:
		/// if flags & 1 : frame32 has been filled
		/// if flags & 2 : frame64 has been filled
		/// if flags & 4 : frame + palette have been filled
		/// if none of them, display the original frame
		/// </summary>
		public byte flags;
		public uint nocolors; // number of shades of orange in the ROM
		public uint ntriggers; // number of triggers in the Serum file
		public uint triggerID; // return 0xffff if no trigger for that frame, the ID of the trigger if one is set for that frame
		public uint frameID; // for CDMD ingame tester
		public ushort rotationtimer;
	}
	public class Serum : AbstractConverter, IColoredGray6Source, IColorRotationSource, IFrameEventSource
	{
		public override string Name => "Serum";

		public override IEnumerable<FrameFormat> From { get; } = new [] { FrameFormat.Gray2, FrameFormat.Gray4 };

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredSerumFrame;
		public IObservable<Color[]> GetPaletteChanges() => _paletteChanges;
		public IObservable<FrameEventInit> GetFrameEventInit() => _frameEventInit;
		public IObservable<FrameEvent> GetFrameEvents() => _frameEvents;

		public bool IsLoaded;
		private uint NumTriggersAvailable { get; }

		/// <summary>
		/// Number of colours in the manufacturer's ROM
		/// </summary>
		public readonly uint NumColors;

		// cROM components
		/// <summary>
		/// Frame dimensions in LEDs for both 32P and 64P
		/// </summary>
		private readonly Dimensions _dimensions;
		private readonly Dimensions _dimensions32;
		private readonly Dimensions _dimensions64;

/*		/// <summary>
		/// maximum amount of colour rotations per frame (for both Serum format)
		/// </summary>
		private const int MaxColorRotations = 8;
		private const int MaxColorRotationsNew = 4;
		/// <summary>
		/// current colour rotation state
		/// </summary>
		private readonly byte[] _rotationCurrentPaletteIndex = new byte[64];
		/// <summary>
		/// first colour of the rotation
		/// </summary>
		private readonly byte[] _rotationStartColor = new byte[MaxColorRotations];
		/// <summary>
		/// number of colors in the rotation
		/// </summary>
		private readonly byte[] _rotationNumColors = new byte[MaxColorRotations];
		/// <summary>
		/// current first colour in the rotation
		/// </summary>
		private readonly byte[] _rotationCurrentStartColor = new byte[MaxColorRotations];
		/// <summary>
		/// time interval between 2 rotations in ms
		/// </summary>
		private readonly double[] _rotationIntervalMs = new double[MaxColorRotations];
		/// <summary>
		/// last rotation start time
		/// </summary>
		private readonly DateTime[] _rotationStartTime = new DateTime[MaxColorRotations];*/

		private IDisposable _rotator;

		/// <summary>
		/// A reusable array of rotation colors when computing rotations.
		/// </summary>
		private readonly Color[] _rotationPalette = new Color[64];

		private readonly byte[] _byteFrame;
		private readonly ushort[] _rgb565Frame32;
		private readonly ushort[] _rgb565Frame64;
		private readonly byte[] _bytePalette = new byte[64 * 3];
		private readonly Color[] _colorPalette = new Color[64];
		//private uint _width32 = 0;
		//private uint _width64 = 0;
		private Serum_Version _version;
		private Serum_Frame_Struc _serumFrame;
		private bool _hasRotations;
		private bool _hasRotations32;
		private bool _hasRotations64;

		private readonly FrameEvent _frameEvent = new FrameEvent();
		private bool _frameEventsInitialized;

		private readonly Subject<ColoredFrame> _coloredSerumFrame = new Subject<ColoredFrame>();

		private readonly Subject<Color[]> _paletteChanges = new Subject<Color[]>();
		private readonly Subject<FrameEventInit> _frameEventInit = new Subject<FrameEventInit>();
		private readonly Subject<FrameEvent> _frameEvents = new Subject<FrameEvent>();

		public ScalerMode ScalerMode { get; set; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Maximum amount of color rotations per frame (both Serum format)
		/// </summary>
		private const int MAX_COLOR_ROTATIONS_V1 = 8;
		private const int MAX_COLOR_ROTATIONS_V2 = 4;
		private const int MAX_LENGTH_COLOR_ROTATION = 64; // maximum number of new colors in a rotation
		public const int FLAG_REQUEST_32P_FRAMES = 1; // there is an output DMD which is 32 leds high
		public const int FLAG_REQUEST_64P_FRAMES = 2; // there is an output DMD which is 64 leds high
		public const int FLAG_32P_FRAME_OK = 1; // the 32p frame has been filled
		public const int FLAG_64P_FRAME_OK = 2; // the 64p frame has been filled
		private IntPtr DLLSerumPtr; // a pointer to the serum structure in the DLL

		public Serum(string altcolorPath, string romName, byte flags) : base (false)
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

			DLLSerumPtr = Serum_Load(altcolorPath, romName, flags);
			if (DLLSerumPtr == null) {
				IsLoaded = false;
				return;
			}
			_serumFrame = (Serum_Frame_Struc)Marshal.PtrToStructure(DLLSerumPtr, typeof(Serum_Frame_Struc));
			NumColors = _serumFrame.nocolors;
			numTriggers = _serumFrame.ntriggers;
			_dimensions32 = new Dimensions((int)_serumFrame.width32, 32);
			_dimensions64 = new Dimensions((int)_serumFrame.width64, 64);
			NumTriggersAvailable = _serumFrame.ntriggers;
			IsLoaded = true;
			if (_serumFrame.SerumVersion == (uint)Serum_Version.SERUM_V1) {
				_version = Serum_Version.SERUM_V1;
				if (_serumFrame.width32 > 0) {
					_dimensions = new Dimensions((int)_serumFrame.width32, 32);
					_byteFrame = new byte[_serumFrame.width32 * 32];
				} else {
					_dimensions = new Dimensions((int)_serumFrame.width64, 64);
					_byteFrame = new byte[_serumFrame.width64 * 64];
				}
			} else {
				_version = Serum_Version.SERUM_V2;
				if (_serumFrame.width32 > 0) {
					_rgb565Frame32 = new ushort[_serumFrame.width32 * 32];
				}
				if (_serumFrame.width64 > 0) {
					_rgb565Frame64 = new ushort[_serumFrame.width64 * 64];
				}
			}
			Logger.Info($"[serum] Found {numTriggers} triggers to emit.");
		}
		public new void Dispose()
		{
			base.Dispose();
			Serum_Dispose();
			StopRotating();
			_frameEventInit?.Dispose();
			_frameEvents?.Dispose();
			IsLoaded = false;
		}

		public override bool Supports(FrameFormat format)
		{
			switch (format) {
				case FrameFormat.Gray4 when NumColors == 16:
				case FrameFormat.Gray2:
					return true;
				default:
					return false;
			}
		}

		public override void Convert(DmdFrame frame)
		{
			if (!_frameEventsInitialized) {
				_frameEventInit.OnNext(new FrameEventInit(NumTriggersAvailable > 0));
				_frameEventsInitialized = true;
			}

			uint isrot = Serum_Colorize(frame.Data);
			_serumFrame = (Serum_Frame_Struc)Marshal.PtrToStructure(DLLSerumPtr, typeof(Serum_Frame_Struc));
			if (_version == Serum_Version.SERUM_V1) {
				Marshal.Copy(_serumFrame.palette, _bytePalette, 0, _bytePalette.Length);
				Marshal.Copy(_serumFrame.frame, _byteFrame, 0, _dimensions.Width * _dimensions.Height);
				// send the colored frame
				_coloredSerumFrame.OnNext(new ColoredFrame(_dimensions, _byteFrame, ConvertPalette()));

				if (isrot != 0xffffffff && isrot > 0) _hasRotations = true;

				if (_serumFrame.triggerID != 0xFFFFFFFF) {
					_frameEvents.OnNext(_frameEvent.Update((ushort)_serumFrame.triggerID));
				}

				if (_hasRotations) {
					StartRotating();
				} else {
					StopRotating();
				}
			} else { // Serum_Version.SERUM_V2
				_hasRotations32 = false;
				_hasRotations64 = false;
				if (_serumFrame.width32 > 0 && _serumFrame.width64 > 0) {
					byte[] tdata = new byte[_rgb565Frame32.Length * 2];
					Marshal.Copy(_serumFrame.frame32, tdata, 0, tdata.Length);
					Buffer.BlockCopy(tdata, 0, _rgb565Frame32, 0, tdata.Length);
					tdata = new byte[_rgb565Frame64.Length * 2];
					Marshal.Copy(_serumFrame.frame64, tdata, 0, tdata.Length);
					Buffer.BlockCopy(tdata, 0, _rgb565Frame64, 0, tdata.Length);
					_coloredSerumFrame.OnNext(new ColoredFrame(_dimensions32, _dimensions64, _rgb565Frame32, _rgb565Frame64, true));
					ushort[] trot = new ushort[MAX_COLOR_ROTATIONS_V2 * MAX_LENGTH_COLOR_ROTATION];
					tdata = new byte[trot.Length * 2];
					Marshal.Copy(_serumFrame.rotations32, tdata, 0, tdata.Length);
					Buffer.BlockCopy(tdata, 0, trot, 0, tdata.Length);
					for (uint ti = 0; ti < MAX_COLOR_ROTATIONS_V2; ti++) {
						if (trot[ti * 3] > 0) {
							_hasRotations32 = true;
						}
					}
					Marshal.Copy(_serumFrame.rotations64, tdata, 0, tdata.Length);
					Buffer.BlockCopy(tdata, 0, trot, 0, tdata.Length);
					for (uint ti = 0; ti < MAX_COLOR_ROTATIONS_V2; ti++) {
						if (trot[ti * 3] > 0) {
							_hasRotations64 = true;
						}
					}
				} else {
					if (_serumFrame.width32 > 0) {
						byte[] tdata = new byte[_rgb565Frame32.Length * 2];
						Marshal.Copy(_serumFrame.frame32, tdata, 0, tdata.Length);
						Buffer.BlockCopy(tdata, 0, _rgb565Frame32, 0, tdata.Length);
						_coloredSerumFrame.OnNext(new ColoredFrame(_dimensions32, _dimensions64, _rgb565Frame32, null, true));
						ushort[] trot = new ushort[MAX_COLOR_ROTATIONS_V2 * MAX_LENGTH_COLOR_ROTATION];
						tdata = new byte[trot.Length * 2];
						Marshal.Copy(_serumFrame.rotations32, tdata, 0, tdata.Length);
						Buffer.BlockCopy(tdata, 0, trot, 0, tdata.Length);
						for (uint ti = 0; ti < MAX_COLOR_ROTATIONS_V2; ti++) {
							if (trot[ti * 3] > 0) {
								_hasRotations32 = true;
							}
						}
					}
					if (_serumFrame.width64 > 0) {
						byte[] tdata = new byte[_rgb565Frame64.Length * 2];
						Marshal.Copy(_serumFrame.frame64, tdata, 0, tdata.Length);
						Buffer.BlockCopy(tdata, 0, _rgb565Frame64, 0, tdata.Length);
						_coloredSerumFrame.OnNext(new ColoredFrame(_dimensions32, _dimensions64, null, _rgb565Frame64, true));
						ushort[] trot = new ushort[MAX_COLOR_ROTATIONS_V2 * MAX_LENGTH_COLOR_ROTATION];
						tdata = new byte[trot.Length * 2];
						Marshal.Copy(_serumFrame.rotations64, tdata, 0, tdata.Length);
						Buffer.BlockCopy(tdata, 0, trot, 0, tdata.Length);
						for (uint ti = 0; ti < MAX_COLOR_ROTATIONS_V2; ti++) {
							if (trot[ti * 3] > 0) {
								_hasRotations64 = true;
							}
						}
					}
				}
				if (_serumFrame.triggerID != 0xffffffff) {
					_frameEvents.OnNext(_frameEvent.Update((ushort)_serumFrame.triggerID));
				}
				if (_hasRotations32 || _hasRotations64) {
					StartRotating();
				} else {
					StopRotating();
				}
			}
		}

		private void Rotate(long _)
		{
			UpdateRotations();
		}

		private void StartRotating()
		{
			if (_rotator != null) {
				return;
			}
			_rotator = Observable
				.Interval(TimeSpan.FromMilliseconds(1d/60))
				.Subscribe(Rotate);
		}

		private void StopRotating()
		{
			if (_rotator == null) {
				return;
			}
			_rotator.Dispose();
			_rotator = null;
		}

		private void UpdateRotations()
		{
			uint changed = Serum_Rotate();
			_serumFrame = (Serum_Frame_Struc)Marshal.PtrToStructure(DLLSerumPtr, typeof(Serum_Frame_Struc));
			if ((changed & (0x10000 + 0x20000)) > 0) {
				if (_version == Serum_Version.SERUM_V1) {
					Marshal.Copy(_serumFrame.palette, _bytePalette, 0, _bytePalette.Length);
					_coloredSerumFrame.OnNext(new ColoredFrame(_dimensions, _byteFrame, ConvertPalette()));
				} else { //Serum_Version.SERUM_V2
					if ((changed & (0x10000 + 0x20000)) == (0x10000 + 0x20000)) {
						byte[] tdata = new byte[_rgb565Frame32.Length * 2];
						Marshal.Copy(_serumFrame.frame32, tdata, 0, tdata.Length);
						Buffer.BlockCopy(tdata, 0, _rgb565Frame32, 0, tdata.Length);
						tdata = new byte[_rgb565Frame64.Length * 2];
						Marshal.Copy(_serumFrame.frame64, tdata, 0, tdata.Length);
						Buffer.BlockCopy(tdata, 0, _rgb565Frame64, 0, tdata.Length);
						_coloredSerumFrame.OnNext(new ColoredFrame(_dimensions32, _dimensions64, _rgb565Frame32, _rgb565Frame64, false));
					} else {
						if ((changed | 0x10000) > 0) { // there is a rotation in the 32P frame
							byte[] tdata = new byte[_rgb565Frame32.Length * 2];
							Marshal.Copy(_serumFrame.frame32, tdata, 0, tdata.Length);
							Buffer.BlockCopy(tdata, 0, _rgb565Frame32, 0, tdata.Length);
							_coloredSerumFrame.OnNext(new ColoredFrame(_dimensions32, _dimensions64, _rgb565Frame32, null, false));
						}
						if ((changed | 0x20000) > 0) { // there is a rotation in the 64P frame
							byte[] tdata = new byte[_rgb565Frame64.Length * 2];
							Marshal.Copy(_serumFrame.frame64, tdata, 0, tdata.Length);
							Buffer.BlockCopy(tdata, 0, _rgb565Frame64, 0, tdata.Length);
							_coloredSerumFrame.OnNext(new ColoredFrame(_dimensions32, _dimensions64, null, _rgb565Frame64, false));
						}
					}
				}
			}
		}

		public static string GetVersion()
		{
			IntPtr pointer = Serum_GetMinorVersion();
			string str = Marshal.PtrToStringAnsi(pointer);
			return str;
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
		/// Serum_Load: Function to call at table opening time
		/// </summary>
		/// <param name="altcolorpath">path of the altcolor directory, e.g. "c:/Visual Pinball/VPinMame/altcolor/"</param>
		/// <param name="romname">rom name</param>
		/// <param name="flags">out: a combination of FLAG_32P_FRAME_OK and FLAG_64P_FRAME_OK according to what has been filled</param>
		/// <returns></returns>
		#if PLATFORM_X64
				[DllImport("serum64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
				[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		// C format: Serum_Frame_Struc* Serum_Load(const char* const altcolorpath, const char* const romname,  const UINT8 flags);
		private static extern IntPtr Serum_Load(string altcolorpath, string romname, byte flags);
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
		/// <summary>
		/// Colorize a frame
		/// </summary>
		/// <param name="frame">the inbound frame with [0-3]/[0-15] indices</param>
		/// <returns></returns>
		#if PLATFORM_X64
				[DllImport("serum64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
				[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		// C format: int Serum_Colorize(UINT8* frame)
		private static extern uint Serum_Colorize(byte[] frame);
		/// <summary>
		/// Apply the rotations
		/// </summary>
		/// <returns></returns>
		#if PLATFORM_X64
				[DllImport("serum64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
				[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		// C format: int Serum_Rotate(void)
		private static extern uint Serum_Rotate();
		#endregion
	}
}
