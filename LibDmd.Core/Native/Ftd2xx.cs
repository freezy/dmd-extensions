using System;
using System.Runtime.InteropServices;

namespace LibDmd.Native
{
	/// <summary>
	/// Minimal FTDI D2XX P/Invoke surface for the PinDMD1 driver. A thin direct binding over the
	/// cross-platform D2XX library (FTDI ships <c>ftd2xx.dll</c> on Windows and
	/// <c>libftd2xx.{dylib,so}</c> on macOS/Linux), replacing the Windows-only managed
	/// <c>FTD2XX_NET</c> wrapper used by the legacy build. Resolved per-OS by
	/// <see cref="NativeLibraryLoader"/> (net) or Unity's Plugins folders (netstandard2.1).
	/// </summary>
	/// <remarks>
	/// The default <see cref="CallingConvention.Winapi"/> matches D2XX: stdcall on Windows, cdecl
	/// elsewhere. On macOS Apple's built-in VCP driver can claim the device — see the §12 caveat.
	/// </remarks>
	internal static class Ftd2xx
	{
		private const string Lib = "ftd2xx";

		public const uint Ok = 0; // FT_OK
		public const uint OpenBySerialNumber = 1; // FT_OPEN_BY_SERIAL_NUMBER

		[DllImport(Lib)]
		public static extern uint FT_CreateDeviceInfoList(out uint numDevs);

		[DllImport(Lib)]
		public static extern uint FT_GetDeviceInfoDetail(uint index, out uint flags, out uint type,
			out uint id, out uint locId, byte[] serialNumber, byte[] description, out IntPtr ftHandle);

		[DllImport(Lib, CharSet = CharSet.Ansi)]
		public static extern uint FT_OpenEx(string arg1, uint flags, out IntPtr ftHandle);

		[DllImport(Lib)]
		public static extern uint FT_Close(IntPtr ftHandle);

		[DllImport(Lib)]
		public static extern uint FT_Write(IntPtr ftHandle, byte[] buffer, uint bytesToWrite, out uint bytesWritten);

		[DllImport(Lib)]
		public static extern uint FT_SetBaudRate(IntPtr ftHandle, uint baudRate);

		[DllImport(Lib)]
		public static extern uint FT_SetBitMode(IntPtr ftHandle, byte mask, byte mode);
	}
}
