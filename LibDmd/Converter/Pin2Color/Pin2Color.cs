using System;
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

namespace LibDmd.Converter.Pin2Color
{
	public class Pin2Color : AbstractSource, IConverter, IColoredGraySource
	{
		public override string Name => "Pin2Color";
		public FrameFormat From { get; } = FrameFormat.ColoredGray;

		private readonly Subject<ColoredFrame> _coloredGrayAnimationFrames = new Subject<ColoredFrame>();

		public bool ScaleToHd = false;
		public ScalerMode ScalerMode { get; set; }

		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool IsOpen = false;
		public bool IsLoaded = false;
		public bool IsColored = false;
		
		private static int width = 128;
		private static int height = 32;

		private static uint lastEventID = 0;

		private static PinUpOutput _activePinUpOutput = null;
		private static ColorizerMode _pin2ColorizerMode;

		private static bool _hasEvents { get; set; }

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

		public IObservable<ColoredFrame> GetColoredGrayFrames() => _coloredGrayAnimationFrames;


		public Pin2Color(bool colorize, string altcolorPath, string gameName, byte red, byte green, byte blue, Color[] palette, ScalerMode ScalerMode, bool ScaleToHd) {

			this.ScalerMode = ScalerMode;
			this.ScaleToHd = ScaleToHd;

			if (!IsLoaded) {
				if (!Pin2Color_Load()) {
					IsLoaded = false;
					return;
				} else {
					IsLoaded = true;
				}
			}

			if (!IsOpen) {
				if (!System.Convert.ToBoolean(ColorizeOpen())) {
					Logger.Info($"[Pin2Color] Failed to open colorizer ...");
					IsOpen = false;
					return;
				} else {
					Logger.Info($"[Pin2Color] Successfully opened colorizer ...");
					IsOpen = true;
				}
			}

			ColorizeAltColorPath(altcolorPath);
			var colorizationPath = Path.Combine(altcolorPath, gameName);
			Logger.Info($"[Pin2Color] Looking for colorization at {colorizationPath} ...");

			PMoptions options = new PMoptions { Red = red, Green = green, Blue = blue, Colorize = colorize ? 1 : 0 };
			IntPtr opt = Marshal.AllocHGlobal(Marshal.SizeOf(options));
			Marshal.StructureToPtr(options, opt, false);
			_pin2ColorizerMode = (ColorizerMode)ColorizeGameSettings(gameName, 0, opt); ;
			if (_pin2ColorizerMode >= 0) {
				Logger.Info($"[Pin2Color] {_pin2ColorizerMode.ToString()} colorization loaded ...");
				IsColored = true;
			} else {
				Logger.Info($"[Pin2Color] No colorization found. Switching to Passthrough.");
				IsColored = false;
			}

			if (IsColored) {
				_hasEvents = System.Convert.ToBoolean(ColorizeHasEvents());
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
			if (_pin2ColorizerMode == ColorizerMode.Advanced128x32) {
				Width = 128;
			} else if (_pin2ColorizerMode == ColorizerMode.Advanced192x64) {
				Width = 192;
			} else if (_pin2ColorizerMode == ColorizerMode.Advanced256x64) {
				Width = 256;
			}
			width = Width;
			return width;
		}

		public int GetHeight(int Height)
		{
			if (_pin2ColorizerMode == ColorizerMode.Advanced128x32) {
				Height = 32;
			} else if (_pin2ColorizerMode == ColorizerMode.Advanced192x64) {
				Height = 64;
			} else if (_pin2ColorizerMode == ColorizerMode.Advanced256x64) {
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
			if (IsOpen && _pin2ColorizerMode >= 0) {
				if (_pin2ColorizerMode == ColorizerMode.Advanced128x32) {
					width = 128;
					height = 32;
				} else if (_pin2ColorizerMode == ColorizerMode.Advanced192x64) {
					width = 192;
					height = 64;
				} else if (_pin2ColorizerMode == ColorizerMode.Advanced256x64) {
					width = 256;
					height = 64;
				} else {
					_pin2ColorizerMode = ColorizerMode.SimplePalette;
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
						if (_pin2ColorizerMode != ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					} else {
						if (frame.Data.Length == 128 * 32)
							Rgb24Buffer = Colorize2GrayWithRaw(128, 32, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						else if (frame.Data.Length == 192 * 64)
							Rgb24Buffer = Colorize2GrayWithRaw(192, 64, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						else if (frame.Data.Length == 256 * 64)
							Rgb24Buffer = Colorize2GrayWithRaw(256, 64, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
						if (_pin2ColorizerMode != ColorizerMode.None)
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
						if (_pin2ColorizerMode != ColorizerMode.None)
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
						if (_pin2ColorizerMode != ColorizerMode.None)
							Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
					} else {
						ColorizeRGB24((ushort)frame.width, (ushort)frame.height, frame.Data);
						return;
					}
				}

				if (_pin2ColorizerMode != ColorizerMode.None) {
					if (ScaleToHd) {
						if (width == 128 && height == 32) {
							width = 256;
							height = 64;
						}
					}
					// send the colored frame
					_coloredGrayAnimationFrames.OnNext(new ColoredFrame(width, height, coloredFrame));
					processEvent();
				}
			}
		}

		public void Convert(AlphaNumericFrame frame)
		{
			int Width = 128;
			int Height = 32;

			var frameSize = Width * Height * 3;
			var coloredFrame = new byte[frameSize];

			int width = Width;
			int height = Height;

			var Rgb24Buffer = ColorizeAlphaNumeric(frame.SegmentLayout, frame.SegmentData, frame.SegmentDataExtended);
			if (_pin2ColorizerMode != Pin2Color.ColorizerMode.None) {
				Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
				if (_pin2ColorizerMode != ColorizerMode.None) {
					if (ScaleToHd) {
						if (width == 128 && height == 32) {
							width *= 2;
							height *= 2;
						}
					}
					// send the colored frame
					_coloredGrayAnimationFrames.OnNext(new ColoredFrame(width, height, coloredFrame));
					processEvent();
				}
			}
		}

		public void ConsoleData(byte data)
		{
			ColorizeConsoleData(data);
		}

		private bool Pin2Color_Load()
		{
			if (!IsLoaded) {
				var localPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
				var assemblyFolder = Path.GetDirectoryName(localPath);
				string dllFileName = null;
				if (IntPtr.Size == 4)
					dllFileName = Path.Combine(assemblyFolder, "PIN2COLOR.DLL");
				else if (IntPtr.Size == 8)
					dllFileName = Path.Combine(assemblyFolder, "PIN2COLOR64.DLL");

				var pDll = NativeDllLoad.LoadLibrary(dllFileName);

				if (pDll == IntPtr.Zero) {
					Logger.Error("No coloring " + dllFileName + " found");
					return false;
				}

				// Now load function calls using PinMame BSD-3 licensed DmdDevice.DLL API
				// See README.MD in PinMameDevice folder.

				try {
					var pAddress = NativeDllLoad.GetProcAddress(pDll, "Open");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeOpen = (_dColorizeOpen)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeOpen));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "PM_GameSettings");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeGameSettings = (_dColorizeGameSettings)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeGameSettings));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Render_4_Shades");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					Colorize2Gray = (_dColorize2Gray)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize2Gray));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Render_16_Shades");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					Colorize4Gray = (_dColorize4Gray)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize4Gray));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Render_4_Shades_with_Raw");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					Colorize2GrayWithRaw = (_dColorize2GrayWithRaw)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize2GrayWithRaw));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Render_16_Shades_with_Raw");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					Colorize4GrayWithRaw = (_dColorize4GrayWithRaw)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize4GrayWithRaw));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Render_RGB24");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeRGB24 = (_dColorizeRGB24)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeRGB24));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Render_PM_Alphanumeric_Frame");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeAlphaNumeric = (_dColorizeAlphaNumeric)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeAlphaNumeric));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Close");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeClose = (_dColorizeClose)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeClose));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Console_Data");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeConsoleData = (_dColorizeConsoleData)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeConsoleData));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Set_4_Colors_Palette");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeSet_4_Colors = (_dColorizeSet_4_Colors)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeSet_4_Colors));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Set_16_Colors_Palette");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeSet_16_Colors = (_dColorizeSet_16_Colors)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeSet_16_Colors));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Get_Event");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeGetEvent = (_dColorizeGetEvent)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeGetEvent));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Has_Events");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeHasEvents = (_dColorizeHasEvents)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeHasEvents));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "PM_AltColorPath");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeAltColorPath = (_dColorizeAltColorPath)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeAltColorPath));

					pAddress = NativeDllLoad.GetProcAddress(pDll, "Get_Version");
					if (pAddress == IntPtr.Zero) {
						throw new Exception("Cannot map function in " + dllFileName);
					}
					ColorizeGetVersion = (_dColorizeGetVersion)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeGetVersion));
				}
				catch (Exception e) {
					Logger.Error(e, "[Pin2Color] Error sending to " + dllFileName + " - disabling.");
					return false;
				}

				Logger.Info("[Pin2Color] Successfully loaded colorizer plugin ...");
			}

			return true;

		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate byte _dColorizeOpen();
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
		private delegate byte _dColorizeClose();
		private static _dColorizeClose ColorizeClose;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _dColorizeConsoleData(byte data);
		private static _dColorizeConsoleData ColorizeConsoleData;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate uint _dColorizeGetEvent();
		private static _dColorizeGetEvent ColorizeGetEvent;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate byte _dColorizeHasEvents();
		private static _dColorizeHasEvents ColorizeHasEvents;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _dColorizeAltColorPath(string Path);
		private static _dColorizeAltColorPath ColorizeAltColorPath;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr _dColorizeGetVersion();
		private static _dColorizeGetVersion ColorizeGetVersion;

		private static void processEvent()
		{
			if (_activePinUpOutput != null) {
				uint eventID = ColorizeGetEvent();
				if (eventID != lastEventID) {
					lastEventID = eventID;
					_activePinUpOutput.SendTriggerID((ushort)eventID);
				}
			}
		}

		public void SetPinUpOutput(PinUpOutput puo)
		{
			_activePinUpOutput = puo;
			if ((puo != null) && _hasEvents) {
				puo.PuPFrameMatching = false;
			}
		}

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
