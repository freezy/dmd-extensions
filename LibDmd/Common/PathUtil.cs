using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using NLog;

namespace LibDmd.Common
{
	public static class PathUtil
	{
		private static readonly string AssemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Returns an existing path to a subfolder relative to the VPM installation.
		/// </summary>
		/// <param name="subfolder">name of the subfolder ("altcolor", "dmddump", etc)</param>
		/// <param name="logPrefix">How to prefix the log messages during search</param>
		/// <returns>Existing, absolute path, or null if not found</returns>
		public static string GetVpmPath(string subfolder, string logPrefix)
		{
			// first, try executing assembly.
			var absPath = Path.Combine(AssemblyPath, subfolder);
			if (Directory.Exists(absPath)) {
				Logger.Info($"{logPrefix} Determined {subfolder} path from assembly path: {absPath}");
				return absPath;
			}

			// then, try vpinmame location
			var vpmDllName = IntPtr.Size == 4  ? "VPinMAME.dll" : "VPinMAME64.dll";
			var vpmPath = GetDllPath(vpmDllName);
			if (vpmPath != null) {
				absPath = Path.Combine(Path.GetDirectoryName(vpmPath), subfolder);
				if (Directory.Exists(absPath)) {
					Logger.Info($"{logPrefix} Determined {subfolder} path from {vpmDllName} location: {absPath}");
					return absPath;
				}
			}

			// then, try vpinmame from the COM registration
			RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\VPinMAME.Controller\CLSID");
			if (reg != null) {
				var clsid = reg.GetValue(null).ToString();
				var x64Suffix = Environment.Is64BitOperatingSystem ? @"WOW6432Node\" : "";
				reg = Registry.ClassesRoot.OpenSubKey(x64Suffix + @"CLSID\" + clsid + @"\InprocServer32");
				if (reg != null) {
					absPath = Path.Combine(Path.GetDirectoryName(reg.GetValue(null).ToString()), subfolder);
					if (Directory.Exists(absPath)) {
						Logger.Info($"{logPrefix} Determined {subfolder} path from VPinMAME registry: {absPath}");
						return absPath;
					}
				}
			}

			Logger.Info($"No {subfolder} folder found.");
			return null;
		}

		private static string GetDllPath(string name)
		{
			const int maxPath = 260;
			var builder = new StringBuilder(maxPath);
			var hModule = GetModuleHandle(name);
			if (hModule == IntPtr.Zero)
			{
				return null;
			}
			var size = GetModuleFileName(hModule, builder, builder.Capacity);
			return size <= 0 ? null : builder.ToString();
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr GetModuleHandle(string lpModuleName);

		[DllImport("kernel32.dll", SetLastError = true)]
		[PreserveSig]
		public static extern uint GetModuleFileName
		(
			[In] IntPtr hModule,
			[Out] StringBuilder lpFilename,
			[In][MarshalAs(UnmanagedType.U4)] int nSize
		);
	}
}
