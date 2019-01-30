using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Installer.Actions
{
	public class OsUtil
	{
		public static bool Is64BitOperatingSystem = (IntPtr.Size == 8) || InternalCheckIsWow64();

		private static bool InternalCheckIsWow64()
		{
			if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
			    Environment.OSVersion.Version.Major >= 6) {
				using (var p = Process.GetCurrentProcess()) {
					return IsWow64Process(p.Handle, out var retVal) && retVal;
				}
			}
			return false;
		}

		[DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool wow64Process);
	}
}
