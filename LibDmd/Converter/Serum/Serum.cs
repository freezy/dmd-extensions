using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter.Serum
{
	public class Serum : AbstractConverter, IColoredGray6Source, IColorRotationSource, IFrameEventSource
	{
		public override string Name => "Serum";

		public override IEnumerable<FrameFormat> From { get; } = new [] { FrameFormat.Gray2, FrameFormat.Gray4 };

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6AnimationFrames;
		public IObservable<Color[]> GetPaletteChanges() => _paletteChanges;
		public IObservable<FrameEventInit> GetFrameEventInit() => _frameEventInit;
		public IObservable<FrameEvent> GetFrameEvents() => _frameEvents;

		/// <summary>
		/// There is an output DMD which is 32 leds high
		/// </summary>
		public const int FlagRequest32PFrames = 1;

		/// <summary>
		/// there is an output DMD which is 64 leds high
		/// </summary>
		public const int FlagRequest64PFrames = 2;


		public bool IsLoaded;
		private uint NumTriggersAvailable { get; }
		public string ColorizationVersion => _serumVersion == SerumVersion.Version1 ? "v1" : _serumVersion == SerumVersion.Version2 ? "v2" : "unknown";

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
		/// A reusable array of rotation colors when computing rotations.
		/// </summary>
		private readonly Color[] _rotationPalette = new Color[64];

		private IDisposable _rotator;

		private readonly Color[] _colorPalette = new Color[64];
		private readonly byte[] _bytePalette = new byte[64 * 3];
		private readonly DmdFrame _frame;
		private readonly FrameEvent _frameEvent = new FrameEvent();
		private bool _frameEventsInitialized;

		private readonly Subject<ColoredFrame> _coloredGray6AnimationFrames = new Subject<ColoredFrame>();
		private readonly Subject<Color[]> _paletteChanges = new Subject<Color[]>();
		private readonly Subject<FrameEventInit> _frameEventInit = new Subject<FrameEventInit>();
		private readonly Subject<FrameEvent> _frameEvents = new Subject<FrameEvent>();

		/// <summary>
		/// A pointer to the serum structure in the DLL
		/// </summary>
		private readonly IntPtr _serumFramePtr;

		/// <summary>
		/// The last frame data returned by the Serum DLL.
		/// </summary>
		private SerumFrame _serumFrame;

		/// <summary>
		/// The Serum version of the current colorization
		/// </summary>
		private readonly SerumVersion _serumVersion;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Serum(string altcolorPath, string romName, byte flags) : base (false)
		{
#if PLATFORM_X64
			const string dllName = "serum64.dll";
#else
			const string dllName = "serum.dll";
#endif
			
			if (File.Exists(dllName)) {
				Logger.Info($"[serum] Found {dllName} at {Directory.GetCurrentDirectory()}.");
			}

			_serumFramePtr = Serum_Load(altcolorPath, romName, flags);
			if (_serumFramePtr == null) {
				IsLoaded = false;
				return;
			}

			ReadSerumFrame();
			NumColors = _serumFrame.nocolors;
			NumTriggersAvailable = _serumFrame.ntriggers;
			_serumVersion = (SerumVersion)_serumFrame.SerumVersion;

			switch (_serumVersion) {

				case SerumVersion.Version1: {
					_serumVersion = SerumVersion.Version1;
					if (_serumFrame.width32 > 0) {
						_dimensions = new Dimensions((int)_serumFrame.width32, 32);
					} else {
						_dimensions = new Dimensions((int)_serumFrame.width64, 64);
					}

					break;
				}
				case SerumVersion.Version2: {
					_serumVersion = SerumVersion.Version2;
					break;
				}
				default:
					throw new NotSupportedException($"Unsupported Serum version: {_serumFrame.SerumVersion}");
			}

			IsLoaded = true;
			_frame = new DmdFrame(_dimensions, ((int)NumColors).GetBitLength());
			Logger.Info($"[serum] Found {NumTriggersAvailable} triggers to emit.");
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

			// Buffer.BlockCopy(frame.Data, 0, _frame.Data, 0, frame.Data.Length);
			// Serum_Colorize(_frame.Data, _dimensions.Width, _dimensions.Height, _bytePalette, _rotations, ref triggerId);


			// 0 => no rotation
			// 1 - 0xFFFF => time in ms to next rotation
			// 0xFFFFFFFF => frame wasn't colorized
			// 0xFFFFFFFE => same frame as before
			var rotations = Serum_Colorize(frame.Data);
			ReadSerumFrame();

			switch (_serumVersion) {

				case SerumVersion.Version1: {

					// copy data from unmanaged to managed
					Marshal.Copy(_serumFrame.Palette, _bytePalette, 0, _bytePalette.Length);
					Marshal.Copy(_serumFrame.Frame, _frame.Data, 0, _dimensions.Width * _dimensions.Height);
					BytesToColors(_bytePalette, _colorPalette);

					var hasRotations = rotations != 0xffffffff && rotations > 0;

					// send event trigger
					if (_serumFrame.triggerID != 0xffffffff) {
						_frameEvents.OnNext(_frameEvent.Update((ushort)_serumFrame.triggerID));
					}

					// send the colored frame
					_coloredGray6AnimationFrames.OnNext(new ColoredFrame(_dimensions, _frame.Data, _colorPalette));
					if (hasRotations) {
						StartRotating();
					} else {
						StopRotating();
					}

					break;
				}
				case SerumVersion.Version2: {
					break;
				}
				default:
					throw new NotSupportedException($"Unsupported Serum version: {_serumFrame.SerumVersion}");
			}
		}

		private void ReadSerumFrame()
		{
			_serumFrame = (SerumFrame)Marshal.PtrToStructure(_serumFramePtr, typeof(SerumFrame));
		}

		private void Rotate(long _)
		{
			if (UpdateRotations()) {
				_paletteChanges.OnNext(_rotationPalette);
			}
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

		private bool UpdateRotations()
		{
			var changed = Serum_Rotate();
			ReadSerumFrame();
			if ((changed & (0x10000 + 0x20000)) > 0) {
				switch (_serumVersion) {
					case SerumVersion.Version1: {
						Marshal.Copy(_serumFrame.Palette, _bytePalette, 0, _bytePalette.Length);
						BytesToColors(_bytePalette, _rotationPalette);
						return true;
					}
					case SerumVersion.Version2: {
						break;
					}
					default:
						throw new NotSupportedException($"Unsupported Serum version: {_serumFrame.SerumVersion}");
				}
			}

			return false;
		}

		public static string GetVersion()
		{
			IntPtr pointer = Serum_GetMinorVersion();
			string str = Marshal.PtrToStringAnsi(pointer);
			return str;
		}

		private static void BytesToColors(IReadOnlyList<byte> bytes, Color[] palette)
		{
			for (int ti = 0; ti < 64; ti++) {
				palette[ti].A = 255;
				palette[ti].R = bytes[ti * 3];
				palette[ti].G = bytes[ti * 3 + 1];
				palette[ti].B = bytes[ti * 3 + 2];
			}
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
