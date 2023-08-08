using System;
using System.Diagnostics;
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
		private static string _sha;
		private static string _fullVersion;
		private delegate bool FileOrDirectoryExists(string path);

		/// <summary>
		/// Returns an existing path to a subfolder relative to the VPM installation.
		/// </summary>
		/// <param name="subfolder">Name of the subfolder ("altcolor", "dmddump", etc)</param>
		/// <param name="logPrefix">How to prefix the log messages during search</param>
		/// <returns>Existing, absolute path, or null if not found</returns>
		public static string GetVpmFolder(string subfolder, string logPrefix) =>
			GetVpmFileOrFolder(subfolder, logPrefix, Directory.Exists);

		/// <summary>
		/// Returns an existing path to a file at the VPM folder.
		/// </summary>
		/// <param name="filename">Name of the file, can be the entire path as well</param>
		/// <param name="logPrefix">How to prefix the log messages during search</param>
		/// <returns>Existing, absolute path, or null if not found</returns>
		public static string GetVpmFile(string filename, string logPrefix) =>
			GetVpmFileOrFolder(filename, logPrefix, File.Exists);

		private static string GetVpmFileOrFolder(string fileOrFolder, string logPrefix, FileOrDirectoryExists exists)
		{
			fileOrFolder = Path.GetFileName(fileOrFolder);

			// first, try executing assembly.
			var absPath = Path.Combine(AssemblyPath, fileOrFolder);
			if (exists(absPath)) {
				Logger.Info($"{logPrefix} Determined {fileOrFolder} path from assembly path: {absPath}");
				return absPath;
			}

			// then, try vpinmame location
			var vpmDllName = IntPtr.Size == 4  ? "VPinMAME.dll" : "VPinMAME64.dll";
			var vpmPath = GetDllPath(vpmDllName);
			if (vpmPath != null) {
				absPath = Path.Combine(Path.GetDirectoryName(vpmPath), fileOrFolder);
				if (exists(absPath)) {
					Logger.Info($"{logPrefix} Determined {fileOrFolder} path from {vpmDllName} location: {absPath}");
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
					absPath = Path.Combine(Path.GetDirectoryName(reg.GetValue(null).ToString()), fileOrFolder);
					if (exists(absPath)) {
						Logger.Info($"{logPrefix} Determined {fileOrFolder} path from VPinMAME registry: {absPath}");
						return absPath;
					}
				}
			}

			Logger.Info($"{logPrefix} {fileOrFolder} not found.");
			return null;
		}

		public static void GetAssemblyVersion(out string fullVersion, out string sha)
		{
			if (_fullVersion != null) {
				fullVersion = _fullVersion;
				sha = _sha;
				return;
			}

			// read versions from assembly
			var assembly = Assembly.GetCallingAssembly();
			var assemblyLocation = GetAssemblyLocation(assembly);

			if (assemblyLocation == null) {
				Logger.Warn($"Unable to determine assembly location.");
				_fullVersion = "<unable to find assembly>";
				_sha = "";

			} else {
				var fvi = FileVersionInfo.GetVersionInfo(assemblyLocation);
				var version = fvi.ProductVersion;
				var attr = assembly.GetCustomAttributes(typeof(AssemblyConfigurationAttribute), false);
				if (attr.Length > 0) {
					var aca = (AssemblyConfigurationAttribute)attr[0];
					_sha = aca.Configuration;
					_fullVersion = string.IsNullOrEmpty(_sha) ? version : $"{version} ({_sha})";

				} else {
					_fullVersion = fvi.ProductVersion;
					_sha = "";
				}
			}

			fullVersion = _fullVersion;
			sha = _sha;
		}

		public static string GetAssemblyPath() => Path.GetDirectoryName(GetAssemblyLocation(Assembly.GetCallingAssembly()));

		private static string GetAssemblyLocation(Assembly assembly)
		{
			if (assembly == null) {
				return null;
			}
			if (!string.IsNullOrEmpty(assembly.Location)) {
				return assembly.Location;
			}

			if (!assembly.CodeBase.ToLowerInvariant().StartsWith("file:")) {
				return null;
			}

			var uri = new UriBuilder(assembly.CodeBase);
			return Uri.UnescapeDataString(uri.Path);
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
