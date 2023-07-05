// ReSharper disable InvertIf

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter.Plugin
{
	public class ColorizationPlugin : AbstractConverter, IColoredGray2Source, IColoredGray4Source,
		IColoredGray6Source, IRgb24Source, IAlphaNumericSource, IFrameEventSource
	{
		public override string Name => "Colorization Plugin";
		public override IEnumerable<FrameFormat> From => new [] { FrameFormat.Gray2, FrameFormat.Gray4, FrameFormat.AlphaNumeric };

		public IObservable<ColoredFrame> GetColoredGray2Frames() => DedupedColoredGray2Source.GetColoredGray2Frames();
		public IObservable<ColoredFrame> GetColoredGray4Frames() => DedupedColoredGray4Source.GetColoredGray4Frames();
		public IObservable<ColoredFrame> GetColoredGray6Frames() => DedupedColoredGray6Source.GetColoredGray6Frames();
		public IObservable<DmdFrame> GetRgb24Frames() => DedupedRgb24Source.GetRgb24Frames();
		public IObservable<AlphaNumericFrame> GetAlphaNumericFrames() => _alphaNumericFrames;
		public IObservable<FrameEventInit> GetFrameEventInit() => _frameEventInit;
		public IObservable<FrameEvent> GetFrameEvents() => _frameEvents;

		private readonly Dimensions _dimensions = Dimensions.Dynamic;

		/// <summary>
		/// DLL has been initialized (Open returned true).
		/// </summary>
		public bool IsAvailable { get; private set; }

		/// <summary>
		/// Whether the plugin has found colorization data and will output colored frames.
		/// </summary>
		public bool IsColoring => _colorizerMode >= 0;

		private bool EmitFrames => _passthrough || IsColoring;

		/// <summary>
		/// If true, the plugin has events that should be sent to PinUp.
		/// </summary>
		private bool _hasEvents;

		/// <summary>
		/// If set, forward all frames to the plugin.
		/// </summary>
		private readonly bool _passthrough;

		private readonly Subject<AlphaNumericFrame> _alphaNumericFrames = new Subject<AlphaNumericFrame>();
		private readonly Subject<FrameEventInit> _frameEventInit = new Subject<FrameEventInit>();
		private readonly Subject<FrameEvent> _frameEvents = new Subject<FrameEvent>();

		private uint _lastEventId;
		private ColorizerMode _colorizerMode = ColorizerMode.None;
		private readonly FrameEvent _frameEvent = new FrameEvent();
		private bool _frameEventsInitialized;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ColorizationPlugin(PluginConfig pluginConfig, bool colorize, string altcolorPath, string gameName,
			Color defaultColor, IReadOnlyList<Color> defaultPalette) : base(true)
		{
			if (pluginConfig == null) {
				return;
			}

			// load plugin
			if (!LoadPlugin(pluginConfig.Path)) {
				return;
			}

			// open plugin
			if (!_open()) {
				Logger.Info($"[plugin] Failed to open colorizer plugin {GetName()} v{GetVersion()}");
				return;
			}
			IsAvailable = true;
			_passthrough = pluginConfig.PassthroughEnabled;

			Logger.Info($"[plugin] Successfully opened colorizer plugin at {pluginConfig.Path}");

			// configure plugin
			_setAltColorPath(altcolorPath);
			PMoptions options = new PMoptions { Red = defaultColor.R, Green = defaultColor.G, Blue = defaultColor.B, Colorize = colorize ? 1 : 0 };
			IntPtr optionsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(options));
			Marshal.StructureToPtr(options, optionsPtr, false);
			_colorizerMode = (ColorizerMode)_setGameSettings(gameName, 0, optionsPtr);
			if (IsColoring) {
				_hasEvents = _hasEventsPtr();

				// dmd frames might return upscaled, so adapt size accordingly
				switch (_colorizerMode) {
					case ColorizerMode.Advanced192x64:
						_dimensions = new Dimensions(192, 64);
						break;
					case ColorizerMode.Advanced256x64:
						_dimensions = new Dimensions(256, 64);
						break;
					case ColorizerMode.Advanced128x32:
						_dimensions = new Dimensions(128, 32);
						break;
					case ColorizerMode.None:
					case ColorizerMode.SimplePalette:
						// no dimension changes
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				Logger.Info($"[plugin] Colorization mode {_colorizerMode} enabled.");

			} else if (_passthrough && defaultPalette != null) {

				// for passthrough, relay palette from VPM to plugin
				var pal = new byte[defaultPalette.Count * 3];
				for (int i = 0; i < defaultPalette.Count; i++) {
					pal[i * 3] = defaultPalette[i].R;
					pal[(i * 3) + 1] = defaultPalette[i].G;
					pal[(i * 3) + 2] = defaultPalette[i].B;
				}
				switch (defaultPalette.Count) {
					case 4:
						_set4Colors(pal);
						break;
					case 16:
						_set16Colors(pal);
						break;
				}
			}

			switch (IsColoring) {
				case false when _passthrough:
					Logger.Info("[plugin] No colorization mode detected, using passthrough mode.");
					break;
				case false:
					Logger.Info("[plugin] No colorization mode detected, disabled.");
					break;
			}
		}

		public new void Dispose()
		{
			base.Dispose();

			if (IsAvailable) {
				_hasEvents = false;
				_close();
			}

			_frameEventInit?.Dispose();
			_frameEvents?.Dispose();

			_colorizerMode = ColorizerMode.None;
			IsAvailable = false;
		}

		public string GetVersion()
		{
			IntPtr pointer = _getVersion();
			string str = Marshal.PtrToStringAnsi(pointer);
			return str;
		}

		public string GetName()
		{
			IntPtr pointer = _getName();
			string str = Marshal.PtrToStringAnsi(pointer);
			return str;
		}

		public void ConsoleData(byte data)
		{
			_onConsoleData(data);
		}

		private void ProcessEvent()
		{
			if (!_frameEventsInitialized) {
				_frameEventInit.OnNext(new FrameEventInit(_hasEvents));
				_frameEventsInitialized = true;
			}

			uint eventId = _getEvent();
			if (eventId == _lastEventId) {
				return;
			}

			_lastEventId = eventId;
			_frameEvents.OnNext(_frameEvent.Update((ushort)eventId));
		}

		#region Conversion

		private byte[] _frameData;
		private readonly Dictionary<int, int> _colorIndex = new Dictionary<int, int>();
		private readonly Color[] _palette = new Color[64];

		/// <summary>
		/// The public API to convert a frame and output it to the pubs.
		/// </summary>
		/// <param name="frame">Uncolored frame with in <see cref="FrameFormat"/>.</param>
		protected override void ConvertClocked(DmdFrame frame)
		{
			var rgb24FramePtr = frame is RawFrame rawFrame && rawFrame.RawPlanes.Length > 0
				? ColorizeFrame(rawFrame)
				: ColorizeFrame(frame);

			if (rgb24FramePtr == IntPtr.Zero || !EmitFrames) {
				return;
			}

			EmitFrame(_dimensions == Dimensions.Dynamic ? frame.Dimensions : _dimensions, rgb24FramePtr);
			ProcessEvent();
		}

		protected override void ConvertClocked(AlphaNumericFrame frame)
		{
			var rgb24FramePtr = _colorizeAlphaNumeric(frame.SegmentLayout, frame.SegmentData, frame.SegmentDataExtended);

			if (_passthrough) {
				_alphaNumericFrames.OnNext(frame);
			}

			if (rgb24FramePtr == IntPtr.Zero || !EmitFrames) {
				return;
			}

			EmitFrame(new Dimensions(128, 32), rgb24FramePtr);
			ProcessEvent();
		}

		private IntPtr ColorizeFrame(RawFrame frame)
		{
			var planeSize = frame.PlaneSize;
			var rawBuffer = new byte[frame.TotalPlanes * planeSize];
			for (int i = 0; i < frame.TotalPlanes; i++) {
				if (i < frame.RawPlanes.Length) {
					frame.RawPlanes[i].CopyTo(rawBuffer, i * planeSize);
				} else {
					frame.ExtraRawPlanes[frame.RawPlanes.Length - i].CopyTo(rawBuffer, i * planeSize);
				}
			}

			switch (frame.BitLength) {
				case 4:
					return _colorizeGray4Raw(
						(ushort)frame.Dimensions.Width, 
						(ushort)frame.Dimensions.Height, 
						frame.Data, 
						(ushort)frame.TotalPlanes,
						rawBuffer
					);
				default: {
					return _colorizeGray2Raw(
						(ushort)frame.Dimensions.Width, 
						(ushort)frame.Dimensions.Height, 
						frame.Data, 
						(ushort)frame.TotalPlanes,
						rawBuffer
					);
				}
			}
		}

		private IntPtr ColorizeFrame(DmdFrame frame)
		{
			switch (frame.BitLength) {
				case 4: return _colorizeGray4((ushort)frame.Dimensions.Width, (ushort)frame.Dimensions.Height, frame.Data);
				case 2: return _colorizeGray2((ushort)frame.Dimensions.Width, (ushort)frame.Dimensions.Height, frame.Data);
				case 24:
					if (_passthrough) {
						_colorizeRgb24((ushort)frame.Dimensions.Width, (ushort)frame.Dimensions.Height, frame.Data);
						DedupedRgb24Source.NextFrame(frame);
					}
					return IntPtr.Zero;

				default:
					throw new ArgumentException($"Plugin does not support {frame.BitLength} bit planes.");
			}
		}

		private void EmitFrame(Dimensions dim, IntPtr rgb24FramePtr)
		{
			var frameSize = dim.Surface * 3;
			var rgb24Frame = new byte[frameSize];
			Marshal.Copy(rgb24FramePtr, rgb24Frame, 0, frameSize);

			if (_frameData == null || _frameData.Length != dim.Surface) {
				_frameData = new byte[dim.Surface];
			}
			_colorIndex.Clear();
			for (var k = 0; k < 64; k++) {
				_palette[k] = Colors.Black;
			}

			var len = dim.Surface * 3;
			var lastIndex = -1;
			var j = 0;
			for (var i = 0; i < len; i += 3) {
				var color = rgb24Frame[i] << 16 | rgb24Frame[i + 1] << 8 | rgb24Frame[i + 2];
				int index;
				if (!_colorIndex.ContainsKey(color)) {
					lastIndex++;
					if (lastIndex > 63) { // break out of the loop, since that's an rgb24 frame now.
						break;
					}
					_colorIndex[color] = lastIndex;
					_palette[lastIndex] = Color.FromRgb(rgb24Frame[i], rgb24Frame[i + 1], rgb24Frame[i + 2]);
					index = lastIndex;
				} else {
					index = _colorIndex[color];
				}

				_frameData[j++] = (byte)index;
			}

			// split and send
			if (lastIndex < 4) {
				DedupedColoredGray2Source.NextFrame(new ColoredFrame(dim, _frameData, _palette.Take(4).ToArray()));

			} else if (lastIndex < 16) {
				DedupedColoredGray4Source.NextFrame(new ColoredFrame(dim, _frameData, _palette.Take(16).ToArray()));

			} else if (lastIndex < 64) {
				DedupedColoredGray6Source.NextFrame(new ColoredFrame(dim, _frameData, _palette));

			} else {
				DedupedRgb24Source.NextFrame(new DmdFrame(dim, rgb24Frame, 24));
			}
		}

		#endregion

		#region Plugin API

		private bool LoadPlugin(string dllPath)
		{
			var dll = NativeDllLoad.LoadLibrary(dllPath);
			if (dll == IntPtr.Zero) {
				var dllFallbackPath = PathUtil.GetVpmFile(dllPath, "[plugin]");
				dll = NativeDllLoad.LoadLibrary(dllFallbackPath);
				if (dll == IntPtr.Zero) {
					Logger.Error("[plugin] Error loading plugin at " + dllPath + ".");
					return false;
				}
			}

			// Now load function calls using PinMame BSD-3 licensed DmdDevice.DLL API
			// See README.MD in PinMameDevice folder.
			try {
				var addr = NativeDllLoad.GetProcAddress(dll, "Open");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Open in " + dllPath);
				}
				_open = (OpenPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(OpenPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "PM_GameSettings");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function PM_GameSettings in " + dllPath);
				}
				_setGameSettings = (SetGameSettingsPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(SetGameSettingsPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Render_4_Shades");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Render_4_Shades in " + dllPath);
				}
				_colorizeGray2 = (ColorizeGray2Ptr)Marshal.GetDelegateForFunctionPointer(addr, typeof(ColorizeGray2Ptr));

				addr = NativeDllLoad.GetProcAddress(dll, "Render_16_Shades");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Render_16_Shades in " + dllPath);
				}
				_colorizeGray4 = (ColorizeGray4Ptr)Marshal.GetDelegateForFunctionPointer(addr, typeof(ColorizeGray4Ptr));

				addr = NativeDllLoad.GetProcAddress(dll, "Render_4_Shades_with_Raw");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Render_4_Shades_with_Raw in " + dllPath);
				}
				_colorizeGray2Raw = (ColorizeGray2RawPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(ColorizeGray2RawPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Render_16_Shades_with_Raw");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Render_16_Shades_with_Raw in " + dllPath);
				}
				_colorizeGray4Raw = (ColorizeGray4RawPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(ColorizeGray4RawPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Render_RGB24");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Render_RGB24 in " + dllPath);
				}
				_colorizeRgb24 = (ColorizeRgb24Ptr)Marshal.GetDelegateForFunctionPointer(addr, typeof(ColorizeRgb24Ptr));

				addr = NativeDllLoad.GetProcAddress(dll, "Render_PM_Alphanumeric_Frame");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Render_PM_Alphanumeric_Frame in " + dllPath);
				}
				_colorizeAlphaNumeric = (ColorizeAlphaNumericPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(ColorizeAlphaNumericPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Close");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Close in " + dllPath);
				}
				_close = (ClosePtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(ClosePtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Console_Data");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Console_Data in " + dllPath);
				}
				_onConsoleData = (OnConsoleDataPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(OnConsoleDataPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Set_4_Colors_Palette");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Set_4_Colors_Palette in " + dllPath);
				}
				_set4Colors = (Set4ColorsPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(Set4ColorsPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Set_16_Colors_Palette");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Set_16_Colors_Palette in " + dllPath);
				}
				_set16Colors = (Set16ColorsPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(Set16ColorsPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Get_Event");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Get_Event in " + dllPath);
				}
				_getEvent = (GetEventPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(GetEventPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Has_Events");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Has_Events in " + dllPath);
				}
				_hasEventsPtr = (HasEventsPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(HasEventsPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "PM_AltColorPath");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function PM_AltColorPath in " + dllPath);
				}
				_setAltColorPath = (SetAltColorPathPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(SetAltColorPathPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Get_Version");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Get_Version in " + dllPath);
				}
				_getVersion = (GetVersionPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(GetVersionPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "Get_Name");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function Get_Name in " + dllPath);
				}
				_getName = (GetNamePtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(GetNamePtr));

			} catch (Exception e) {
				Logger.Error($"[plugin] Error loading plugin, disabling: {e.Message}");
				return false;
			}
			return true;
		}


		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool OpenPtr();
		private OpenPtr _open;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void Set4ColorsPtr(byte[] palette);
		private Set4ColorsPtr _set4Colors;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void Set16ColorsPtr(byte[] palette);
		private Set16ColorsPtr _set16Colors;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeGray4Ptr(ushort width, ushort height, byte[] currBuffer);
		private ColorizeGray4Ptr _colorizeGray4;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeGray2Ptr(ushort width, ushort height, byte[] currBuffer);
		private ColorizeGray2Ptr _colorizeGray2;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeGray4RawPtr(ushort width, ushort height, byte[] currBuffer, ushort numRawFrames, byte[] currRawBuffer);
		private ColorizeGray4RawPtr _colorizeGray4Raw;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeGray2RawPtr(ushort width, ushort height, byte[] currBuffer, ushort numRawFrames, byte[] currRawBuffer);
		private ColorizeGray2RawPtr _colorizeGray2Raw;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void ColorizeRgb24Ptr(ushort width, ushort height, byte[] currBuffer);
		private ColorizeRgb24Ptr _colorizeRgb24;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeAlphaNumericPtr(NumericalLayout numericalLayout, ushort[] segData, ushort[] segData2);
		private ColorizeAlphaNumericPtr _colorizeAlphaNumeric;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int SetGameSettingsPtr(string gameName, ulong hardwareGeneration, IntPtr options);
		private SetGameSettingsPtr _setGameSettings;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool ClosePtr();
		private ClosePtr _close;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void OnConsoleDataPtr(byte data);
		private OnConsoleDataPtr _onConsoleData;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate uint GetEventPtr();
		private GetEventPtr _getEvent;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool HasEventsPtr();
		private HasEventsPtr _hasEventsPtr;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void SetAltColorPathPtr(string path);
		private SetAltColorPathPtr _setAltColorPath;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr GetVersionPtr();
		private GetVersionPtr _getVersion;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr GetNamePtr();
		private GetNamePtr _getName;

		#endregion
	}
	
	public enum ColorizerMode
	{
		None = -1,
		SimplePalette = 0,
		Advanced128x32 = 1,
		Advanced192x64 = 3,
		Advanced256x64 = 4,
	}
	
	static class NativeDllLoad
	{
		[DllImport("kernel32.dll")]
		public static extern IntPtr LoadLibrary(string dllToLoad);

		[DllImport("kernel32.dll")]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

		[DllImport("kernel32.dll")]
		public static extern bool FreeLibrary(IntPtr hModule);
	}
}
