using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using LibDmd.DmdDevice;
using LibDmd.Output.PinUp;
using NLog;

namespace LibDmd.Converter.Pin2Color
{

	public class Pin2Color 
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static bool _colorizerIsOpen = false;
		public static bool _colorizerIsLoaded = false;

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
		
		public static bool SetColorize()
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

		public static int Init(bool colorize, string gameName, byte red, byte green, byte blue)
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
