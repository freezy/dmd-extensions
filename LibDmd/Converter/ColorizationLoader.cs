using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using Microsoft.Win32;
using NLog;

namespace LibDmd.Converter
{
	public class ColorizationLoader
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly string AssemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
		private string _altcolorPath;

		public ColorizationLoader()
		{
			_altcolorPath = GetColorPath();
		}

		public Pin2Color.Pin2Color LoadPin2Color(bool colorize, string gameName, byte red, byte green, byte blue, Color[] palette, ScalerMode ScalerMode, bool ScaleToHd)
		{
			if (_altcolorPath == null) {
				return null;
			}

			var pin2color = new Pin2Color.Pin2Color(colorize, _altcolorPath, gameName, red, green, blue, palette, ScalerMode, ScaleToHd);
			if (pin2color.IsLoaded) {
				Logger.Info($"[Pin2Color] Colorizer v{pin2color.GetVersion()} initialized.");
				return pin2color;
			}

			return null;
		}

		public Serum.Serum LoadSerum(string gameName, ScalerMode scalerMode)
		{
			if (_altcolorPath == null)
			{
				return null;
			}

			var serumPath = Path.Combine(_altcolorPath, gameName, gameName + ".cRZ");
			if (File.Exists(serumPath))
			{
				try
				{
					var serum = new Serum.Serum(_altcolorPath, gameName);
					if (serum.IsLoaded)
					{
						Logger.Info($"Serum colorizer v{Serum.Serum.GetVersion()} initialized.");
						Logger.Info($"Loading colorization at {serumPath}...");
						Analytics.Instance.SetColorizer("Serum");
						serum.ScalerMode = scalerMode;
						return serum;
					}

					Logger.Warn($"Found Serum coloring file at {serumPath}, but could not load colorizer.");

				}
				catch (Exception e)
				{
					Logger.Warn(e, "Error initializing colorizer: {0}", e.Message);
					Analytics.Instance.ClearColorizer();
				}
			} else {
				Analytics.Instance.ClearColorizer();
			}

			return null;
		}

		private static string GetColorPath()
		{
			// first, try executing assembly.
			var altcolor = Path.Combine(AssemblyPath, "altcolor");
			if (Directory.Exists(altcolor))
			{
				Logger.Info("Determined color path from assembly path: {0}", altcolor);
				return altcolor;
			}

			// then, try vpinmame location
			var vpmPath = GetDllPath("VPinMAME.dll");
			if (vpmPath != null)
			{
				altcolor = Path.Combine(Path.GetDirectoryName(vpmPath), "altcolor");
				if (Directory.Exists(altcolor))
				{
					Logger.Info("Determined color path from VPinMAME.dll location: {0}", altcolor);
					return altcolor;
				}
			}

			// then, try vpinmame from the COM registration
			RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\VPinMAME.Controller\CLSID");
			if (reg != null)
			{
				var clsid = reg.GetValue(null).ToString();

				var x64Suffix = Environment.Is64BitOperatingSystem ? @"WOW6432Node\" : "";
				reg = Registry.ClassesRoot.OpenSubKey(x64Suffix + @"CLSID\" + clsid + @"\InprocServer32");
				if (reg != null)
				{
					altcolor = Path.Combine(Path.GetDirectoryName(reg.GetValue(null).ToString()), "altcolor");
					if (Directory.Exists(altcolor))
					{
						Logger.Info("Determined color path from VPinMAME registry: {0}", altcolor);
						return altcolor;
					}


				}
			}

			Logger.Info("No altcolor folder found, ignoring palettes.");
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
