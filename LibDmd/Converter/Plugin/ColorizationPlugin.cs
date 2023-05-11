using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Output.PinUp;
using NLog;

namespace LibDmd.Converter.Plugin
{
	public class ColorizationPlugin : AbstractSource, IConverter, IColoredGray6Source
	{
		public override string Name => "Colorization Plugin";
		public FrameFormat From => FrameFormat.Gray2;

		public readonly bool ScaleToHd;
		public ScalerMode ScalerMode { get; set; }

		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6Frames;
		
		public bool IsOpen;
		public bool IsLoaded;
		public bool IsColored;
		
		private int _width = 128;
		private int _height = 32;

		private uint _lastEventId;
		private PinUpOutput _activePinUpOutput;
		private ColorizerMode _colorizerMode;
		
		private readonly Subject<ColoredFrame> _coloredGray6Frames = new Subject<ColoredFrame>();

		private static bool HasEvents { get; set; }
		
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ColorizationPlugin(string pluginPath, bool colorize, string altcolorPath, string gameName, Color defaultColor, IReadOnlyList<Color> palette, ScalerMode scalerMode, bool scaleToHd) {

			this.ScalerMode = scalerMode;
			this.ScaleToHd = scaleToHd;

			if (!IsLoaded) {
				if (!LoadPlugin(pluginPath)) {
					IsLoaded = false;
					return;
				}
				IsLoaded = true;
			}

			if (!IsOpen) {
				if (!_open()) {
					Logger.Info("[plugin] Failed to open colorizer plugin.");
					IsOpen = false;
					return;
				}

				Logger.Info($"[plugin] Successfully opened colorizer plugin at {pluginPath}");
				IsOpen = true;
			}

			_getAltColorPath(altcolorPath);
			PMoptions options = new PMoptions { Red = defaultColor.R, Green = defaultColor.G, Blue = defaultColor.B, Colorize = colorize ? 1 : 0 };
			IntPtr opt = Marshal.AllocHGlobal(Marshal.SizeOf(options));
			Marshal.StructureToPtr(options, opt, false);
			_colorizerMode = (ColorizerMode)_setGameSettings(gameName, 0, opt); ;
			if (_colorizerMode >= 0) {
				Logger.Info($"[plugin] Colorization mode {_colorizerMode.ToString()} enabled.");
				IsColored = true;
			} else {
				Logger.Info("[plugin] No colorization mode detected, disabled.");
				IsColored = false;
			}

			if (IsColored) {
				HasEvents = _hasEvents();
			}

			if (palette != null && !IsColored) {
				var _pal = new byte[palette.Count * 3];
				for (int i = 0; i < palette.Count; i++) {
					_pal[i * 3] = palette[i].R;
					_pal[(i * 3) + 1] = palette[i].G;
					_pal[(i * 3) + 2] = palette[i].B;
				}
				if (palette.Count == 4) {
					_set4Colors(_pal);
				} else if (palette.Count == 16) {
					_set16Colors(_pal);
				}
			}
		}
		public void Init()
		{
		}

		public void Dispose()
		{
			if (IsOpen) {
				_activePinUpOutput = null;
				HasEvents = false;
				_close();
			}
			IsColored = false;
			IsOpen = false;
		}

		public int GetWidth(int width)
		{
			if (_colorizerMode == ColorizerMode.Advanced128x32) {
				width = 128;
			} else if (_colorizerMode == ColorizerMode.Advanced192x64) {
				width = 192;
			} else if (_colorizerMode == ColorizerMode.Advanced256x64) {
				width = 256;
			}
			_width = width;
			return _width;
		}

		public int GetHeight(int height)
		{
			if (_colorizerMode == ColorizerMode.Advanced128x32) {
				height = 32;
			} else if (_colorizerMode == ColorizerMode.Advanced192x64) {
				height = 64;
			} else if (_colorizerMode == ColorizerMode.Advanced256x64) {
				height = 64;
			}
			_height = height;
			return _height;
		}

		public static string GetVersion()
		{
			IntPtr pointer = _getVersion();
			string str = Marshal.PtrToStringAnsi(pointer);
			return str;
		}
		
		public static string GetName()
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
			if (_activePinUpOutput == null) {
				return;
			}

			uint eventId = _getEvent();
			if (eventId == _lastEventId) {
				return;
			}

			_lastEventId = eventId;
			_activePinUpOutput.SendTriggerID((ushort)eventId);
		}

		public void SetPinUpOutput(PinUpOutput puo)
		{
			_activePinUpOutput = puo;
			if ((puo != null) && HasEvents) {
				puo.PuPFrameMatching = false;
			}
		}
		
		public enum ColorizerMode
		{
			None = -1,
			SimplePalette = 0,
			Advanced128x32 = 1,
			Advanced192x64 = 3,
			Advanced256x64 = 4,
		}
		
		#region Conversion
		
		private byte[] _frame;
		private readonly Dictionary<int, int> _colorIndex = new Dictionary<int, int>();
		private readonly Color[] _palette = new Color[64];
		
		public void Convert(DMDFrame frame)
		{
			if (IsOpen && _colorizerMode >= 0) {
				if (_colorizerMode == ColorizerMode.Advanced128x32) {
					_width = 128;
					_height = 32;
				} else if (_colorizerMode == ColorizerMode.Advanced192x64) {
					_width = 192;
					_height = 64;
				} else if (_colorizerMode == ColorizerMode.Advanced256x64) {
					_width = 256;
					_height = 64;
				} else {
					_colorizerMode = ColorizerMode.SimplePalette;
				}
			}

			var frameSize = _width * _height * 3;
			var coloredFrame = new byte[frameSize];

			if (!IsOpen) {
				return;
			}

			IntPtr rgb24FramePtr = IntPtr.Zero;
			if (frame is RawDMDFrame rawFrame && rawFrame.RawPlanes.Length > 0) {
				var rawBuffer = new byte[rawFrame.RawPlanes.Length * rawFrame.RawPlanes[0].Length];
				for (int i = 0; i < rawFrame.RawPlanes.Length; i++) {
					rawFrame.RawPlanes[i].CopyTo(rawBuffer, i * rawFrame.RawPlanes[0].Length);
				}
				if (frame.BitLength == 4) {
					if (frame.Data.Length == 128 * 32)
						rgb24FramePtr = _colorizeGray4Raw(128, 32, frame.Data, (ushort)rawFrame.RawPlanes.Length, rawBuffer);
					else if (frame.Data.Length == 192 * 64)
						rgb24FramePtr = _colorizeGray4Raw(192, 64, frame.Data, (ushort)rawFrame.RawPlanes.Length, rawBuffer);
					else if (frame.Data.Length == 256 * 64)
						rgb24FramePtr = _colorizeGray4Raw(256, 64, frame.Data, (ushort)rawFrame.RawPlanes.Length, rawBuffer);
					if (_colorizerMode != ColorizerMode.None)
						Marshal.Copy(rgb24FramePtr, coloredFrame, 0, frameSize);
				} else {
					if (frame.Data.Length == 128 * 32)
						rgb24FramePtr = _colorizeGray2Raw(128, 32, frame.Data, (ushort)rawFrame.RawPlanes.Length, rawBuffer);
					else if (frame.Data.Length == 192 * 64)
						rgb24FramePtr = _colorizeGray2Raw(192, 64, frame.Data, (ushort)rawFrame.RawPlanes.Length, rawBuffer);
					else if (frame.Data.Length == 256 * 64)
						rgb24FramePtr = _colorizeGray2Raw(256, 64, frame.Data, (ushort)rawFrame.RawPlanes.Length, rawBuffer);
					if (_colorizerMode != ColorizerMode.None)
						Marshal.Copy(rgb24FramePtr, coloredFrame, 0, frameSize);
				}
			} else {
				if (frame.BitLength == 4) {
					if (frame.Data.Length == 128 * 32)
						rgb24FramePtr = _colorizeGray4(128, 32, frame.Data);
					else if (frame.Data.Length == 192 * 64)
						rgb24FramePtr = _colorizeGray4(192, 64, frame.Data);
					else if (frame.Data.Length == 256 * 64)
						rgb24FramePtr = _colorizeGray4(256, 64, frame.Data);
					if (_colorizerMode != ColorizerMode.None)
						Marshal.Copy(rgb24FramePtr, coloredFrame, 0, frameSize);
				} else if (frame.BitLength == 2) {
					if (frame.Data.Length == 128 * 16)
						rgb24FramePtr = _colorizeGray2(128, 16, frame.Data);
					else if (frame.Data.Length == 128 * 32)
						rgb24FramePtr = _colorizeGray2(128, 32, frame.Data);
					else if (frame.Data.Length == 192 * 64)
						rgb24FramePtr = _colorizeGray2(192, 64, frame.Data);
					else if (frame.Data.Length == 256 * 64)
						rgb24FramePtr = _colorizeGray2(256, 64, frame.Data);
					if (_colorizerMode != ColorizerMode.None)
						Marshal.Copy(rgb24FramePtr, coloredFrame, 0, frameSize);
				} else {
					_colorizeRgb24((ushort)frame.width, (ushort)frame.height, frame.Data);
					return;
				}
			}

			if (_colorizerMode == ColorizerMode.None) {
				return;
			}

			if (ScaleToHd) {
				if (_width == 128 && _height == 32) {
					_width = 256;
					_height = 64;
				}
			}
					
			EmitFrame(_width, _height, coloredFrame);
			ProcessEvent();
		}

		public void Convert(AlphaNumericFrame frame)
		{
			int Width = 128;
			int Height = 32;
		
			var frameSize = Width * Height * 3;
			var coloredFrame = new byte[frameSize];
		
			int width = Width;
			int height = Height;
		
			var rgb24Buffer = _colorizeAlphaNumeric(frame.SegmentLayout, frame.SegmentData, frame.SegmentDataExtended);
			if (_colorizerMode != ColorizerMode.None) {
				Marshal.Copy(rgb24Buffer, coloredFrame, 0, frameSize);
				if (_colorizerMode != ColorizerMode.None) {
					if (ScaleToHd) {
						if (width == 128 && height == 32) {
							width *= 2;
							height *= 2;
						}
					}
					
					EmitFrame(_width, _height, coloredFrame);
					ProcessEvent();
				}
			}
		}

		private void EmitFrame(int width, int height, IReadOnlyList<byte> rgb24Frame)
		{
			if (_frame == null || _frame.Length != width * height) {
				_frame = new byte[width * height];
			}
			_colorIndex.Clear();
			for (var i = 0; i < 64; i++) {
				_palette[i] = Colors.Black;
			}

			var len = width * height * 3;
			var index = -1;
			var j = 0;
			for (var i = 0; i < len; i += 3) {
				var color = rgb24Frame[i] << 16 | rgb24Frame[i + 1] << 8 | rgb24Frame[i + 2];
				if (!_colorIndex.ContainsKey(color)) {
					index++;
					_colorIndex[color] = ++index;
					_palette[index] = Color.FromRgb(rgb24Frame[i], rgb24Frame[i + 1], rgb24Frame[i + 2]);
				} else {
					index = _colorIndex[color];
				}

				_frame[j++] = (byte)index;
			}

			// split and send
			var planes = FrameUtil.Split(width, height, 6, _frame);
			_coloredGray6Frames.OnNext(new ColoredFrame(planes, _palette));
		}

		#endregion

		#region Plugin API

		private bool LoadPlugin(string dllPath)
		{
			if (IsLoaded) {
				return true;
			}

			if (!File.Exists(dllPath)) {
				Logger.Error("[plugin] Ignoring plugin defined at " + dllPath + ", file does not exist.");
				return false;
			}

			var dll = NativeDllLoad.LoadLibrary(dllPath);

			if (dll == IntPtr.Zero) {
				Logger.Error("[plugin] Error loading plugin at " + dllPath + ".");
				return false;
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
				_hasEvents = (HasEventsPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(HasEventsPtr));

				addr = NativeDllLoad.GetProcAddress(dll, "PM_AltColorPath");
				if (addr == IntPtr.Zero) {
					throw new Exception("Cannot map function PM_AltColorPath in " + dllPath);
				}
				_getAltColorPath = (SetAltColorPathPtr)Marshal.GetDelegateForFunctionPointer(addr, typeof(SetAltColorPathPtr));

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
				Logger.Error($"Error loading plugin, disabling: {e.Message}");
				return false;
			}
			return true;
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool OpenPtr();
		private static OpenPtr _open;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void Set4ColorsPtr(byte[] palette);
		private static Set4ColorsPtr _set4Colors;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void Set16ColorsPtr(byte[] palette);
		private static Set16ColorsPtr _set16Colors;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeGray4Ptr(ushort width, ushort height, byte[] currBuffer);
		private static ColorizeGray4Ptr _colorizeGray4;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeGray2Ptr(ushort width, ushort height, byte[] currBuffer);
		private static ColorizeGray2Ptr _colorizeGray2;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeGray4RawPtr(ushort width, ushort height, byte[] currBuffer, ushort numRawFrames, byte[] currRawBuffer);
		private static ColorizeGray4RawPtr _colorizeGray4Raw;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeGray2RawPtr(ushort width, ushort height, byte[] currBuffer, ushort numRawFrames, byte[] currRawBuffer);
		private static ColorizeGray2RawPtr _colorizeGray2Raw;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void ColorizeRgb24Ptr(ushort width, ushort height, byte[] currBuffer);
		private static ColorizeRgb24Ptr _colorizeRgb24;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr ColorizeAlphaNumericPtr(NumericalLayout numericalLayout, ushort[] segData, ushort[] segData2);
		private static ColorizeAlphaNumericPtr _colorizeAlphaNumeric;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int SetGameSettingsPtr(string gameName, ulong hardwareGeneration, IntPtr options);
		private static SetGameSettingsPtr _setGameSettings;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool ClosePtr();
		private static ClosePtr _close;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void OnConsoleDataPtr(byte data);
		private static OnConsoleDataPtr _onConsoleData;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate uint GetEventPtr();
		private static GetEventPtr _getEvent;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool HasEventsPtr();
		private static HasEventsPtr _hasEvents;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void SetAltColorPathPtr(string path);
		private static SetAltColorPathPtr _getAltColorPath;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr GetVersionPtr();
		private static GetVersionPtr _getVersion;
		
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr GetNamePtr();
		private static GetNamePtr _getName;
		
		#endregion
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
