using System;
using System.Collections.Generic;
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
	public class Serum : AbstractConverter, IColoredGray6Source, IColorRotationSource, IFrameEventSource
	{
		public override string Name => "Serum";

		public override IEnumerable<FrameFormat> From { get; } = new [] { FrameFormat.Gray2, FrameFormat.Gray4 };

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6AnimationFrames;
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
		/// Frame dimensions in LEDs
		/// </summary>
		private readonly Dimensions _dimensions;

		/// <summary>
		/// maximum amount of colour rotations per frame
		/// </summary>
		private const int MaxColorRotations = 8;
		/// <summary>
		/// current colour rotation state
		/// </summary>
		private readonly byte[] _rotationCurrentPaletteIndex = new byte[64];
		/// <summary>
		/// A reusable array of rotation colors when computing rotations.
		/// </summary>
		private readonly Color[] _rotationPalette = new Color[64];
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
		private readonly DateTime[] _rotationStartTime = new DateTime[MaxColorRotations];

		private IDisposable _rotator;

		private readonly Color[] _colorPalette = new Color[64];
		private readonly byte[] _bytePalette = new byte[64 * 3];
		private readonly DmdFrame _frame;
		private readonly FrameEvent _frameEvent = new FrameEvent();
		private bool _frameEventsInitialized;
		private readonly byte[] _rotations;

		private readonly Subject<ColoredFrame> _coloredGray6AnimationFrames = new Subject<ColoredFrame>();
		private readonly Subject<Color[]> _paletteChanges = new Subject<Color[]>();
		private readonly Subject<FrameEventInit> _frameEventInit = new Subject<FrameEventInit>();
		private readonly Subject<FrameEvent> _frameEvents = new Subject<FrameEvent>();

		public ScalerMode ScalerMode { get; set; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Maximum amount of color rotations per frame
		/// </summary>
		private const int MAX_COLOR_ROTATIONS = 8;

		public Serum(string altcolorPath, string romName) : base (false)
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
			_rotations = new byte[MAX_COLOR_ROTATIONS * 3];
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

		public override void Convert(DmdFrame frame)
		{
			if (!_frameEventsInitialized) {
				_frameEventInit.OnNext(new FrameEventInit(NumTriggersAvailable > 0));
				_frameEventsInitialized = true;
			}

			Buffer.BlockCopy(frame.Data, 0, _frame.Data, 0, frame.Data.Length);
			uint triggerId = 0xFFFFFFFF;
			Serum_Colorize(_frame.Data, _dimensions.Width, _dimensions.Height, _bytePalette, _rotations, ref triggerId);

			var hasRotations = false;
			for (uint ti = 0; ti < MAX_COLOR_ROTATIONS; ti++) {
				if ((_rotations[ti * 3] >= 64) || (_rotations[ti * 3] + _rotations[ti * 3 + 1] > 64)) {
					_rotations[ti * 3] = 255;
				} else {
					hasRotations = true;
				}
			}

			// send event trigger
			if (triggerId != 0xFFFFFFFF) {
				_frameEvents.OnNext(_frameEvent.Update((ushort)triggerId));
			}

			// send the colored frame
			_coloredGray6AnimationFrames.OnNext(new ColoredFrame(_dimensions, _frame.Data, ConvertPalette(), hasRotations ? _rotations : null));

			if (hasRotations) {
				for (byte i = 0; i < 64; i++) {
					_rotationCurrentPaletteIndex[i] = i; // init index to be equal to palette
				}
				DateTime now = DateTime.UtcNow;
				for (var i = 0; i < MaxColorRotations; i++) {
					_rotationStartColor[i] = _rotations[i * 3];
					_rotationNumColors[i] = _rotations[i * 3 + 1];
					_rotationIntervalMs[i] = 10.0 * _rotations[i * 3 + 2];
					_rotationStartTime[i] = now;
					_rotationCurrentStartColor[i] = 0;
				}
				StartRotating();

			} else {
				StopRotating();
			}
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
			var changed = false;
			DateTime now = DateTime.UtcNow;
			for (uint i = 0; i < MaxColorRotations; i++) { // for each rotation

				if (_rotationStartColor[i] == 255) { // blank?
					continue;
				}
				if (now.Subtract(_rotationStartTime[i]).TotalMilliseconds < _rotationIntervalMs[i]) { // time to rotate?
					continue;
				}

				_rotationStartTime[i] = now;
				_rotationCurrentStartColor[i]++;
				_rotationCurrentStartColor[i] %= _rotationNumColors[i];
				for (byte j = 0; j < _rotationNumColors[i]; j++) { // for each color in rotation
					var index = _rotationStartColor[i] + j;
					_rotationCurrentPaletteIndex[index] = (byte)(index + _rotationCurrentStartColor[i]);
					if (_rotationCurrentPaletteIndex[index] >= _rotationStartColor[i] + _rotationNumColors[i]) { // cycle?
						_rotationCurrentPaletteIndex[index] -= _rotationNumColors[i];
					}
				}

				for (int j = 0; j < 64; j++) {
					_rotationPalette[j] = _colorPalette[_rotationCurrentPaletteIndex[j]];
				}
				changed = true;
			}

			return changed;
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
		/// <param name="triggernb"></param>
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
		/// <param name="triggerId"></param>
#if PLATFORM_X64
		[DllImport("serum64.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#else
		[DllImport("serum.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
#endif
		// C format: void Serum_Colorize(UINT8* frame, int width, int height, UINT8* palette, UINT8* rotations, UINT32* triggerID)
		private static extern void Serum_Colorize(Byte[] frame, int width, int height, byte[] palette, byte[] rotations, ref uint triggerId);
		
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
