using System;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Input.Passthrough;
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
		public bool IsColored = false ;

		private static uint lastEventID = 0;

		private static PinUpOutput _activePinUpOutput = null;

		public enum ColorizerMode
		{
			None = -1,
			SimplePalette = 0,
			Advanced128x32 = 1,
			Advanced192x64 = 3,
			Advanced256x64 = 4,
		}

		public IObservable<ColoredFrame> GetColoredGrayFrames() => _coloredGrayAnimationFrames;


		public Pin2Color(bool colorize, string altcolorPath, string gameName, byte red, byte green, byte blue, ScalerMode ScalerMode, bool ScaleToHd) {

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

			if(!IsOpen) {
				if (!Open()) {
					IsOpen = false;
					return;
				} else {
					IsOpen = true;
				}
			}

			_pin2ColorizerMode = (ColorizerMode)Setup(colorize, gameName, red, green, blue);
			if (_pin2ColorizerMode >= 0) {
				IsColored = true;
			} else {
				IsColored = false;
			}
		}
		public void Init()
		{
		}
		public void Dispose()
		{
			if (IsOpen) Close();
			IsOpen = false;
		}


		public static bool Pin2Color_Load()
		{
			var localPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
			var assemblyFolder = Path.GetDirectoryName(localPath);
			string dllFileName = null;
			if (IntPtr.Size == 4)
				dllFileName = Path.Combine(assemblyFolder, "PIN2COLOR.DLL");
			else if (IntPtr.Size == 8)
				dllFileName = Path.Combine(assemblyFolder, "PIN2COLOR64.DLL");

			var pDll = NativeDllLoad.LoadLibrary(dllFileName);

			if (pDll == IntPtr.Zero)
			{
				Logger.Error("No coloring " + dllFileName + " found");
				return false;
			}

			try
			{
				var pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeOpen");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeOpen = (_dColorizeOpen)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeOpen));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeInit");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeInit = (_dColorizeInit)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeInit));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "Colorize2Gray");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				Colorize2Gray = (_dColorize2Gray)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize2Gray));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "Colorize4Gray");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				Colorize4Gray = (_dColorize4Gray)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize4Gray));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "Colorize2GrayWithRaw");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				Colorize2GrayWithRaw = (_dColorize2GrayWithRaw)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize2GrayWithRaw));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "Colorize4GrayWithRaw");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				Colorize4GrayWithRaw = (_dColorize4GrayWithRaw)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorize4GrayWithRaw));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeRGB24");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeRGB24 = (_dColorizeRGB24)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeRGB24));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeAlphaNumeric");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeAlphaNumeric = (_dColorizeAlphaNumeric)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeAlphaNumeric));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeClose");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeClose = (_dColorizeClose)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeClose));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeConsoleData");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeConsoleData = (_dColorizeConsoleData)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeConsoleData));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeSet_4_Colors");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeSet_4_Colors = (_dColorizeSet_4_Colors)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeSet_4_Colors));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeSet_16_Colors");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeSet_16_Colors = (_dColorizeSet_16_Colors)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeSet_16_Colors));

				pAddress = NativeDllLoad.GetProcAddress(pDll, "ColorizeGetEvent");
				if (pAddress == IntPtr.Zero)
				{
					throw new Exception("Cannot map function in " + dllFileName);
				}
				ColorizeGetEvent = (_dColorizeGetEvent)Marshal.GetDelegateForFunctionPointer(pAddress, typeof(_dColorizeGetEvent));
			}
			catch (Exception e)
			{
				Logger.Error(e, "[Pin2Color] Error sending to " + dllFileName + " - disabling.");
				return false;
			}

			Logger.Info("Loading Pin2Color plugin ...");

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
		private delegate int _dColorizeInit(bool colorize, string gameName, byte red, byte green, byte blue);
		private static _dColorizeInit ColorizeInit;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate bool _dColorizeClose();
		private static _dColorizeClose ColorizeClose;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void _dColorizeConsoleData(byte data);
		private static _dColorizeConsoleData ColorizeConsoleData;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate uint _dColorizeGetEvent();
		private static _dColorizeGetEvent ColorizeGetEvent;

		private ColorizerMode _pin2ColorizerMode;
		public void Convert(DMDFrame frame)
		{
			int width = frame.width;
			int height = frame.height;

			if (IsOpen && _pin2ColorizerMode >= 0) {
				if (_pin2ColorizerMode == ColorizerMode.Advanced128x32 && ((frame.width == 128 && frame.height == 32) || (frame.width == 128 && frame.height == 16))) {
					width = 128;
					height = 32;
				} else if (_pin2ColorizerMode == ColorizerMode.Advanced192x64 && frame.width == 192 && frame.height == 64) {
					width = 192;
					height = 64;
				} else if (_pin2ColorizerMode == ColorizerMode.Advanced256x64 && ((frame.width == 128 && frame.height == 32) || (frame.width == 256 && frame.height == 64))) {
					width = 256;
					height = 64;
				} else {
					_pin2ColorizerMode = ColorizerMode.SimplePalette;
				}
			}

			var frameSize = width * height * 3;
			var coloredFrame = new byte[frameSize];

			if (IsOpen) {
				
				if (frame is RawDMDFrame vd && vd.RawPlanes.Length > 0) {
					var RawBuffer = new byte[vd.RawPlanes.Length * vd.RawPlanes[0].Length];
					for (int i = 0; i < vd.RawPlanes.Length; i++) {
						vd.RawPlanes[i].CopyTo(RawBuffer, i * vd.RawPlanes[0].Length);
					}
					IntPtr Rgb24Buffer = IntPtr.Zero;
					if (frame.BitLength == 4) 
						Rgb24Buffer = Render4GrayWithRaw((ushort)frame.width, (ushort)frame.height, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);
					else
						Rgb24Buffer = Render2GrayWithRaw((ushort)frame.width, (ushort)frame.height, frame.Data, (ushort)vd.RawPlanes.Length, RawBuffer);

					if (_pin2ColorizerMode != ColorizerMode.None)
						Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
				} else {
					IntPtr Rgb24Buffer = IntPtr.Zero;
					if (frame.BitLength == 4) {
						Rgb24Buffer = Render4Gray((ushort)frame.width, (ushort)frame.height, frame.Data);
					} else if (frame.BitLength == 2) {
						Rgb24Buffer = Render2Gray((ushort)frame.width, (ushort)frame.height, frame.Data);
					} else {
						RenderRGB24((ushort)frame.width, (ushort)frame.height, frame.Data);
						return;
					}

					if (_pin2ColorizerMode != ColorizerMode.None)
						Marshal.Copy(Rgb24Buffer, coloredFrame, 0, frameSize);
				}

				if (_pin2ColorizerMode != ColorizerMode.None) {
					if (ScaleToHd) {
						if (width == 128 && height == 32) {
							width *= 2;
							height *= 2;
						}
					}
					// send the colored frame
					_coloredGrayAnimationFrames.OnNext(new ColoredFrame(width, height, coloredFrame));
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

			var Rgb24Buffer = Pin2Color.RenderAlphaNumeric(frame.SegmentLayout, frame.SegmentData, frame.SegmentDataExtended);
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
				}
			}
		}

		public static bool Open() {
			return ColorizeOpen();
		}

		public static void Set_4_Colors(byte[] palette) {
			ColorizeSet_4_Colors(palette);
		}

		public static void Set_16_Colors(byte[] palette) {
			ColorizeSet_16_Colors(palette);
		}

		public static IntPtr Render4Gray(ushort width, ushort height, byte[] currbuffer) {
			var Rgb24Buffer = Colorize4Gray(width, height, currbuffer);
			processEvent();
			return Rgb24Buffer;
		}

		public static IntPtr Render2Gray(ushort width, ushort height, byte[] currbuffer) {
			var Rgb24Buffer = Colorize2Gray(width, height, currbuffer);
			processEvent();
			return Rgb24Buffer;
		}
		public static IntPtr Render4GrayWithRaw(ushort width, ushort height, byte[] currbuffer, ushort noOfRawFrames, byte[] currrawbuffer)
		{
			var Rgb24Buffer = Colorize4GrayWithRaw(width, height, currbuffer, noOfRawFrames, currrawbuffer);
			processEvent();
			return Rgb24Buffer;
		}

		public static IntPtr Render2GrayWithRaw(ushort width, ushort height, byte[] currbuffer, ushort noOfRawFrames, byte[] currrawbuffer)
		{
			var Rgb24Buffer = Colorize2GrayWithRaw(width, height, currbuffer, noOfRawFrames, currrawbuffer);
			processEvent();
			return Rgb24Buffer;
		}

		public static void RenderRGB24(ushort width, ushort height, byte[] currbuffer)
		{
			ColorizeRGB24(width, height, currbuffer);
		}

		public static IntPtr RenderAlphaNumeric(NumericalLayout numericalLayout, ushort[] seg_data, ushort[] seg_data2)
		{
			var Rgb24Buffer = ColorizeAlphaNumeric(numericalLayout, seg_data, seg_data2);
			processEvent();
			return Rgb24Buffer;
		}

		public int Setup(bool colorize, string gameName, byte red, byte green, byte blue)
		{
			return ColorizeInit(colorize, gameName, red, green, blue);
		}

		public static bool Close()
		{
			_activePinUpOutput = null;
			return ColorizeClose();
		}

		public static void ConsoleData(byte data)
		{
			ColorizeConsoleData(data);
		}

		public static void processEvent()
		{
			uint eventID = ColorizeGetEvent();
			if (eventID != lastEventID) {
				lastEventID = eventID;
				if (_activePinUpOutput != null) _activePinUpOutput.SendTriggerID((ushort)eventID);
			}
		}

		public static void SetPinUpOutput(PinUpOutput puo)
		{
			_activePinUpOutput = puo;
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
