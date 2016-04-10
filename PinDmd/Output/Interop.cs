using System;
using System.Runtime.InteropServices;

namespace PinDmd.Output
{
	class Setting
	{
		public const int Debug = 34;
		public const int Brightness = 35;
		public const int NumShades4 = 36;
		public const int NumShades16 = 37;
		public const int RainbowSpeed = 38;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct Options
	{
		public int DmdRed;
		public int DmdGreen;
		public int DmdBlue;
		public int DmdPerc66;
		public int DmdPerc33;
		public int DmdPerc0;
		public int DmdOnly;
		public int DmdCompact;
		public int DmdAntialias;
		public int DmdColorize;
		public int DmdRed66;
		public int DmdGreen66;
		public int DmdBlue66;
		public int DmdRed33;
		public int DmdGreen33;
		public int DmdBlue33;
		public int DmdRed0;
		public int DmdGreen0;
		public int DmdBlue0;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct PixelRgb24
	{
		public byte Red;
		public byte Green;
		public byte Blue;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	struct DeviceInfo
	{
		public byte Width;
		public byte Height;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
		public string Firmware;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	struct DllInfo
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
		public string Version;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	struct DeviceSettings
	{
		public byte DebugMode;
		public byte Brightness;
		public byte RainbowSpeed;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string Shades4;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
		public string Shades16;
	}

	/// <summary>
	/// P/Invoke signatures of pinDMD's header file
	/// </summary>
	/// <see cref="http://54.148.129.10/pinDMD/downloads-v3/integration/pinDMD3.h">Source</see>
	class Interop
	{
		/// Return Type: int
		/// colours: Options
		[DllImport("pinDMD.dll", EntryPoint = "pindmdInit", CallingConvention = CallingConvention.Cdecl)]
		public static extern int Init(Options colours);

		/// Return Type: void
		[DllImport("pinDMD.dll", EntryPoint = "pindmdDeInit", CallingConvention = CallingConvention.Cdecl)]
		public static extern void DeInit();

		/// Return Type: void
		/// gen: UINT64->unsigned __int64
		/// width: UINT8->unsigned char
		/// height: UINT8->unsigned char
		/// currbuffer: UINT8*
		/// doDumpFrame: UINT8->unsigned char
		[DllImport("pinDMD.dll", EntryPoint = "renderDMDFrame", CallingConvention = CallingConvention.Cdecl)]
		public static extern void RenderDmdFrame(ulong gen, byte width, byte height, IntPtr currbuffer, byte doDumpFrame);

		/// Return Type: void
		/// gen: UINT64->unsigned __int64
		/// seg_data: UINT16*
		/// total_disp: UINT8->unsigned char
		/// disp_lens: UINT8*
		[DllImport("pinDMD.dll", EntryPoint = "renderAlphanumericFrame", CallingConvention = CallingConvention.Cdecl)]
		public static extern void RenderAlphanumericFrame(ulong gen, ref ushort seg_data, byte total_disp, IntPtr disp_lens);

		/// Return Type: void
		/// currbuffer: UINT8*
		[DllImport("pinDMD.dll", EntryPoint = "render16ShadeFrame", CallingConvention = CallingConvention.Cdecl)]
		public static extern void Render16ShadeFrame(IntPtr currbuffer);

		/// Return Type: void
		/// currbuffer: rgb24*
		[DllImport("pinDMD.dll", EntryPoint = "renderRGB24Frame", CallingConvention = CallingConvention.Cdecl)]
		public static extern void RenderRgb24Frame(PixelRgb24[] currbuffer);

		/// Return Type: void
		/// setting: UINT8->unsigned char
		/// params: UINT8*
		[DllImport("pinDMD.dll", EntryPoint = "setSetting", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetSetting(byte setting, IntPtr @params);

		/// Return Type: void
		/// settings: deviceSettings
		[DllImport("pinDMD.dll", EntryPoint = "getSettings", CallingConvention = CallingConvention.Cdecl)]
		public static extern void GetSettings(DeviceSettings settings);

		/// Return Type: void
		/// info: deviceInfo
		[DllImport("pinDMD.dll", EntryPoint = "getDeviceInfo", CallingConvention = CallingConvention.Cdecl)]
		public static extern void GetDeviceInfo(ref DeviceInfo info);

		/// Return Type: void
		/// info: dllInfo
		[DllImport("pinDMD.dll", EntryPoint = "getDllInfo", CallingConvention = CallingConvention.Cdecl)]
		public static extern void GetDllInfo(DllInfo info);

		/// Return Type: void
		[DllImport("pinDMD.dll", EntryPoint = "enableDebug", CallingConvention = CallingConvention.Cdecl)]
		public static extern void EnableDebug();

		/// Return Type: void
		[DllImport("pinDMD.dll", EntryPoint = "disableDebug", CallingConvention = CallingConvention.Cdecl)]
		public static extern void DisableDebug();
	}
}