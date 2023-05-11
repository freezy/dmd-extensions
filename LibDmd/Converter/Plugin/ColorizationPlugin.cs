using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reflection;
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
		public override string Name => "Coloring Plugin";
		public FrameFormat From => FrameFormat.Gray2;

		public readonly bool ScaleToHd;
		public ScalerMode ScalerMode { get; set; }

		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6Frames;
		
		private readonly Subject<ColoredFrame> _coloredGray6Frames = new Subject<ColoredFrame>();

		public bool IsOpen;
		public bool IsLoaded;
		public bool IsColored;
		
		private static int width = 128;
		private static int height = 32;

		private static uint lastEventID = 0;

		private static PinUpOutput _activePinUpOutput = null;
		private static ColorizerMode _colorizerMode;

		private static bool _hasEvents { get; set; }
		
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public enum ColorizerMode
		{
			None = -1,
			SimplePalette = 0,
			Advanced128x32 = 1,
			Advanced192x64 = 3,
			Advanced256x64 = 4,
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct PMoptions
		{
			public int Red, Green, Blue;
			public int Perc66, Perc33, Perc0;
			public int DmdOnly, Compact, Antialias;
			public int Colorize;
			public int Red66, Green66, Blue66;
			public int Red33, Green33, Blue33;
			public int Red0, Green0, Blue0;
		}

		public ColorizationPlugin(string pluginPath, bool colorize, string altcolorPath, string gameName, Color defaultColor, Color[] palette, ScalerMode ScalerMode, bool ScaleToHd) {

			this.ScalerMode = ScalerMode;
			this.ScaleToHd = ScaleToHd;

			if (!IsLoaded) {
				if (!LoadPlugin(pluginPath)) {
					IsLoaded = false;
					return;
				}
				IsLoaded = true;
			}

			if (!IsOpen) {
				if (!ColorizeOpen()) {
					Logger.Info("Failed to open colorizer plugin.");
					IsOpen = false;
					return;
				}

				Logger.Info($"Successfully loaded colorizer plugin at {pluginPath}");
				IsOpen = true;
			}

			ColorizeAltColorPath(altcolorPath);
			PMoptions options = new PMoptions { Red = defaultColor.R, Green = defaultColor.G, Blue = defaultColor.B, Colorize = colorize ? 1 : 0 };
			IntPtr opt = Marshal.AllocHGlobal(Marshal.SizeOf(options));
			Marshal.StructureToPtr(options, opt, false);
			_colorizerMode = (ColorizerMode)ColorizeGameSettings(gameName, 0, opt); ;
			if (_colorizerMode >= 0) {
				Logger.Info($"Plugin colorization mode {_colorizerMode.ToString()} enabled.");
				IsColored = true;
			} else {
				Logger.Info("Plugin has no colorization mode, disabling.");
				IsColored = false;
			}

			if (IsColored) {
				_hasEvents = ColorizeHasEvents();
			}

			if (palette != null && !IsColored) {
				var _pal = new byte[palette.Length * 3];
				for (int i = 0; i < palette.Length; i++) {
					_pal[i * 3] = palette[i].R;
					_pal[(i * 3) + 1] = palette[i].G;
					_pal[(i * 3) + 2] = palette[i].B;
				}
				if (palette.Length == 4) {
					ColorizeSet_4_Colors(_pal);
				} else if (palette.Length == 16) {
					ColorizeSet_16_Colors(_pal);
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
				_hasEvents = false;
				ColorizeClose();
			}
			IsColored = false;
			IsOpen = false;
		}

		public int GetWidth(int Width)
		{
			if (_colorizerMode == ColorizerMode.Advanced128x32) {
				Width = 128;
			} else if (_colorizerMode == ColorizerMode.Advanced192x64) {
				Width = 192;
			} else if (_colorizerMode == ColorizerMode.Advanced256x64) {
				Width = 256;
			}
			width = Width;
			return width;
		}

		public int GetHeight(int Height)
		{
			if (_colorizerMode == ColorizerMode.Advanced128x32) {
				Height = 32;
			} else if (_colorizerMode == ColorizerMode.Advanced192x64) {
				Height = 64;
			} else if (_colorizerMode == ColorizerMode.Advanced256x64) {
				Height = 64;
			}
			height = Height;
			return height;
		}

		public string GetVersion()
		{
			IntPtr pointer = ColorizeGetVersion();
			string str = Marshal.PtrToStringAnsi(pointer);
			return str;
		}

		public void Convert(DMDFrame frame)
		{
			if (IsOpen && _colorizerMode >= 0) {
				if (_colorizerMode == ColorizerMode.Advanced128x32) {
					width = 128;
					height = 32;
				} else if (_colorizerMode == ColorizerMode.Advanced192x64) {
					width = 192;
					height = 64;
				} else if (_colorizerMode == ColorizerMode.Advanced256x64) {
					width = 256;
					height = 64;
				} else {
					_colorizerMode = ColorizerMode.SimplePalette;
				}
			}

			var frameSize = width * height * 3;
			var coloredFrame = new byte[frameSize];

			if (IsOpen) {
				IntPtr Rgb24Buffer = IntPtr.Zero;
				if (frame is RawDMDFrame vd && vd.RawPlanes.Length > 0) {
					var RawBuffer = new byte[vd.RawPlanes.Length * vd.RawPlanes[0].Length];
					for (int i = 0; i < vd.RawPlanes.Length; i++) {
						vd.RawPlanes[i].CopyTo(RawBuffer, i * vd.RawPlanes[0].Length);
					}
					if (frame.BitLength == 4) {
						if (frame.Data.Length == 128 * 32)
							Rgb24Buffer = Colorize4GrayWithRaw(128, 32, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						else if (frame.Data.Length == 192 * 64)
							Rgb24Buffer = Colorize4GrayWithRaw(192, 64, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						else if (frame.Data.Length == 256 * 64)
							Rgb24Buffer = Colorize4GrayWithRaw(256, 64, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						if (_colorizerMode != ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					} else {
						if (frame.Data.Length == 128 * 32)
							Rgb24Buffer = Colorize2GrayWithRaw(128, 32, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						else if (frame.Data.Length == 192 * 64)
							Rgb24Buffer = Colorize2GrayWithRaw(192, 64, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						else if (frame.Data.Length == 256 * 64)
							Rgb24Buffer = Colorize2GrayWithRaw(256, 64, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						if (_colorizerMode != ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					}
				} else {
					if (frame.BitLength == 4) {
						if (frame.Data.Length == 128 * 32)
							Rgb24Buffer = Colorize4Gray(128, 32, frame.Data);
						else if (frame.Data.Length == 192 * 64)
							Rgb24Buffer = Colorize4Gray(192, 64, frame.Data);
						else if (frame.Data.Length == 256 * 64)
							Rgb24Buffer = Colorize4Gray(256, 64, frame.Data);
						if (_colorizerMode != ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					} else if (frame.BitLength == 2) {
						if (frame.Data.Length == 128 * 16)
							Rgb24Buffer = Colorize2Gray(128, 16, frame.Data);
						else if (frame.Data.Length == 128 * 32)
							Rgb24Buffer = Colorize2Gray(128, 32, frame.Data);
						else if (frame.Data.Length == 192 * 64)
							Rgb24Buffer = Colorize2Gray(192, 64, frame.Data);
						else if (frame.Data.Length == 256 * 64)
							Rgb24Buffer = Colorize2Gray(256, 64, frame.Data);
						if (_colorizerMode != ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					} else {
						ColorizeRGB24((ushort)frame.width, (ushort)frame.height, frame.Data);
						return;
					}
				}

				if (_colorizerMode != ColorizerMode.None) {
					if (ScaleToHd) {
						if (width == 128 && height == 32) {
							width = 256;
							height = 64;
						}
					}
					// send the colored frame
					
					_coloredGray6Frames.OnNext(NextFrame(width, height, coloredFrame));
					ProcessEvent();
				}
			}
		}

		private ColoredFrame NextFrame(int w, int h, IReadOnlyList<byte> rgb24Frame)
		{
			var frame = new byte[w * h];
			var palette = new Color[64];
			var dict = new Dictionary<int, int>();
			var len = w * h * 3;
			var index = -1;
			var j = 0;
			for (var i = 0; i < len; i += 3) {
				var color = rgb24Frame[i] << 16 | rgb24Frame[i + 1] << 8 | rgb24Frame[i + 2];
				if (!dict.ContainsKey(color)) {
					index++;
					dict[color] = index;
					palette[index] = Color.FromRgb(rgb24Frame[i], rgb24Frame[i + 1], rgb24Frame[i + 2]);
				} else {
					index = dict[color];
				}

				frame[j] = (byte)index;

				j++;
			}

			var planes = FrameUtil.Split(w, h, 6, frame);
			return new ColoredFrame(planes, palette);
		}

		// public void Convert(AlphaNumericFrame frame)
		// {
		// 	int Width = 128;
		// 	int Height = 32;
		//
		// 	var frameSize = Width * Height * 3;
		// 	var coloredFrame = new byte[frameSize];
		//
		// 	int width = Width;
		// 	int height = Height;
		//
		// 	var Rgb24Buffer = ColorizeAlphaNumeric(frame.SegmentLayout, frame.SegmentData, frame.SegmentDataExtended);
		// 	if (_colorizerMode != ColorizerMode.None) {
		// 		Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
		// 		if (_colorizerMode != ColorizerMode.None) {
		// 			if (ScaleToHd) {
		// 				if (width == 128 && height == 32) {
		// 					width *= 2;
		// 					height *= 2;
		// 				}
		// 			}
		// 			// send the colored frame
		// 			_coloredGrayAnimationFrames.OnNext(new ColoredFrame(width, height, coloredFrame));
		// 			processEvent();
		// 		}
		// 	}
		// }

		public void ConsoleData(byte data)
		{
			ColorizeConsoleData(data);
		}
		
		private static void ProcessEvent()
		{
			if (_activePinUpOutput == null) {
				return;
			}

			uint eventId = ColorizeGetEvent();
			if (eventId == lastEventID) {
				return;
			}

			lastEventID = eventId;
			_activePinUpOutput.SendTriggerID((ushort)eventId);
		}

		public void SetPinUpOutput(PinUpOutput puo)
		{
			_activePinUpOutput = puo;
			if ((puo != null) && _hasEvents) {
				puo.PuPFrameMatching = false;
			}
		}

		#region Plugin API

		private bool LoadPlugin(string dllPath)
		{
			if (IsLoaded) {
				return true;
			}

			if (!File.Exists(dllPath)) {
				Logger.Error("Ignoring plugin at " + dllPath + ", file does not exist.");
				return false;
			}

			var dll = NativeDllLoad.LoadLibrary(dllPath);

			if (dll == IntPtr.Zero) {
				Logger.Error("Error loading plugin at " + dllPath + ".");
				return false;
			}

			// Now load function calls using PinMame BSD-3 licensed DmdDevice.DLL API
			// See README.MD in PinMameDevice folder.
			try {
				var pAddress = NativeDllLoad.GetProcAddress(dll, "Open");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeOpen = (_dColorizeOpen)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeOpen));

				pAddress = NativeDllLoad.GetProcAddress(dll, "PM_GameSettings");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeGameSettings = (_dColorizeGameSettings)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeGameSettings));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Render_4_Shades");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				Colorize2Gray = (_dColorize2Gray)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize2Gray));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Render_16_Shades");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				Colorize4Gray = (_dColorize4Gray)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize4Gray));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Render_4_Shades_with_Raw");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				Colorize2GrayWithRaw = (_dColorize2GrayWithRaw)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize2GrayWithRaw));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Render_16_Shades_with_Raw");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				Colorize4GrayWithRaw = (_dColorize4GrayWithRaw)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize4GrayWithRaw));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Render_RGB24");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeRGB24 = (_dColorizeRGB24)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeRGB24));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Render_PM_Alphanumeric_Frame");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeAlphaNumeric = (_dColorizeAlphaNumeric)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeAlphaNumeric));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Close");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeClose = (_dColorizeClose)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeClose));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Console_Data");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeConsoleData = (_dColorizeConsoleData)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeConsoleData));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Set_4_Colors_Palette");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeSet_4_Colors = (_dColorizeSet_4_Colors)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeSet_4_Colors));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Set_16_Colors_Palette");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeSet_16_Colors = (_dColorizeSet_16_Colors)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeSet_16_Colors));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Get_Event");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeGetEvent = (_dColorizeGetEvent)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeGetEvent));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Has_Events");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeHasEvents = (_dColorizeHasEvents)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeHasEvents));

				pAddress = NativeDllLoad.GetProcAddress(dll, "PM_AltColorPath");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeAltColorPath = (_dColorizeAltColorPath)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeAltColorPath));

				pAddress = NativeDllLoad.GetProcAddress(dll, "Get_Version");
				if (pAddress == IntPtr.Zero) {
					throw new Exception("Cannot map function in " + dllPath);
				}
				ColorizeGetVersion = (_dColorizeGetVersion)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeGetVersion));
				
			} catch (Exception e) {
				Logger.Error($"Error loading plugin, disabling: {e.Message}");
				return false;
			}
			return true;

		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool _dColorizeOpen();
		private static _dColorizeOpen ColorizeOpen;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _dColorizeSet_4_Colors(byte[] palette);
		private static _dColorizeSet_4_Colors ColorizeSet_4_Colors;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _dColorizeSet_16_Colors(byte[] palette);
		private static _dColorizeSet_16_Colors ColorizeSet_16_Colors;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr _dColorize4Gray(ushort width, ushort height, byte[] currbuffer);
		private static _dColorize4Gray Colorize4Gray;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr _dColorize2Gray(ushort width, ushort height, byte[] currbuffer);
		private static _dColorize2Gray Colorize2Gray;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr _dColorize4GrayWithRaw(ushort width, ushort height, byte[] currbuffer, ushort noOfRawFrames, byte[] currrawbuffer);
		private static _dColorize4GrayWithRaw Colorize4GrayWithRaw;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr _dColorize2GrayWithRaw(ushort width, ushort height, byte[] currbuffer, ushort noOfRawFrames, byte[] currrawbuffer);
		private static _dColorize2GrayWithRaw Colorize2GrayWithRaw;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _dColorizeRGB24(ushort width, ushort height, byte[] currbuffer);
		private static _dColorizeRGB24 ColorizeRGB24;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr _dColorizeAlphaNumeric(NumericalLayout numericalLayout, ushort[] seg_data, ushort[] seg_data2);
		private static _dColorizeAlphaNumeric ColorizeAlphaNumeric;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int _dColorizeGameSettings(string gameName, ulong hardwareGeneration, IntPtr options);
		private static _dColorizeGameSettings ColorizeGameSettings;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool _dColorizeClose();
		private static _dColorizeClose ColorizeClose;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _dColorizeConsoleData(byte data);
		private static _dColorizeConsoleData ColorizeConsoleData;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate uint _dColorizeGetEvent();
		private static _dColorizeGetEvent ColorizeGetEvent;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool _dColorizeHasEvents();
		private static _dColorizeHasEvents ColorizeHasEvents;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _dColorizeAltColorPath(string Path);
		private static _dColorizeAltColorPath ColorizeAltColorPath;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr _dColorizeGetVersion();
		private static _dColorizeGetVersion ColorizeGetVersion;
		
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
