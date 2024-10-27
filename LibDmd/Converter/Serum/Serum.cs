using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter.Serum
{
	public class Serum : AbstractConverter, IColoredGray6Source, IRgb565Source, IColorRotationSource, IFrameEventSource
	{
		protected override bool PadSmallFrames => true;
		public override string Name => "Serum";

		public override IEnumerable<FrameFormat> From { get; } = new [] { FrameFormat.Gray2, FrameFormat.Gray4 };

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6Frames;
		public IObservable<DmdFrame> GetRgb565Frames() => _rgb565Frames;
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

		private IDisposable _rotator;

		private bool _frameEventsInitialized;

		private readonly Subject<ColoredFrame> _coloredGray6Frames = new Subject<ColoredFrame>();
		private readonly Subject<DmdFrame> _rgb565Frames = new Subject<DmdFrame>();
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

		/// <summary>
		/// A reusable array of rotation colors when computing rotations.
		/// </summary>
		private readonly Color[] _rotationPalette = new Color[64];

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		internal const int MAX_LENGTH_COLOR_ROTATION = 64; // maximum number of new colors in a rotation

		private readonly ISerumApi _api;

		public Serum(string altcolorPath, string romName, ScalerMode scalerMode) : base (false)
		{
#if PLATFORM_X64
			const string dllName = "serum64.dll";
#else
			const string dllName = "serum.dll";
#endif
			
			if (File.Exists(dllName)) {
				Logger.Info($"[serum] Found {dllName} at {Directory.GetCurrentDirectory()}.");
			}

			_serumFramePtr = Serum_Load(altcolorPath, romName, FlagRequest32PFrames | FlagRequest64PFrames);
			if (_serumFramePtr == null) {
				IsLoaded = false;
				return;
			}

			ReadSerumFrame();
			NumTriggersAvailable = _serumFrame.ntriggers;
			_serumVersion = (SerumVersion)_serumFrame.SerumVersion;

			switch (_serumVersion) {
				case SerumVersion.Version1: {
					_api = new SerumApiV1(_coloredGray6Frames, _frameEvents, ref _serumFrame);
					break;
				}
				case SerumVersion.Version2: {
					_api = new SerumApiV2(_rgb565Frames, scalerMode, ref _serumFrame);
					break;
				}
				default:
					throw new NotSupportedException($"Unsupported Serum version: {_serumFrame.SerumVersion}");
			}

			IsLoaded = true;
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
				case FrameFormat.Gray4 when _api.NumColors == 16 || _api is SerumApiV2:
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

			var rotations = Serum_Colorize(frame.Data);
			ReadSerumFrame();
			var hasRotations = _api.Convert(ref _serumFrame, rotations);

			if (hasRotations) {
				StartRotating();
			} else {
				StopRotating();
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
			try {
				var changed = Serum_Rotate();
				if ((changed & (0x10000 + 0x20000)) <= 0) {
					return false;
				}

				ReadSerumFrame();
				_api.UpdateRotations(ref _serumFrame, _rotationPalette, changed);
				return true;
				
			} catch (Exception) {
				// had a "Attempted to divide by zero." error within Serum_Rotate.
				return false;
			}
		}

		public static string GetVersion()
		{
			IntPtr pointer = Serum_GetMinorVersion();
			string str = Marshal.PtrToStringAnsi(pointer);
			return str;
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
