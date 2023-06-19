using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Plugin;
using LibDmd.DmdDevice;
using Microsoft.Win32;
using NLog;

namespace LibDmd.Converter.Pin2Color
{
	public class ColorizationLoader
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private string _altcolorPath;

		public ColorizationLoader()
		{
			_altcolorPath = PathUtil.GetVpmPath("altcolor", "[serum]");
		}

		public Serum.Serum LoadSerum(string gameName, ScalerMode scalerMode)
		{
			if (_altcolorPath == null) {
				return null;
			}

			var serumPath = Path.Combine(_altcolorPath, gameName, gameName + ".cRZ");
			if (File.Exists(serumPath)) {
				try {
					var serum = new Serum.Serum(_altcolorPath, gameName);
					if (serum.IsLoaded) {
						Logger.Info($"[serum] Serum colorizer v{Serum.Serum.GetVersion()} initialized.");
						Logger.Info($"[serum] Loading colorization at {serumPath}...");
						Analytics.Instance.SetColorizer("Serum");
						serum.ScalerMode = scalerMode;
						return serum;
					}

					Logger.Warn($"[serum] Found Serum coloring file at {serumPath}, but could not load colorizer.");

				} catch (Exception e) {
					Logger.Warn(e, "[serum] Error initializing colorizer: {0}", e.Message);
					Analytics.Instance.ClearColorizer();
				}
			} else {
				Logger.Info($"[serum] No colorization found at {serumPath}...");
				Analytics.Instance.ClearColorizer();
			}

			return null;
		}

		public Pin2ColorResult LoadPin2Color(string gameName, ScalerMode scalerMode)
		{
			if (_altcolorPath == null)
			{
				Analytics.Instance.ClearColorizer();
				return null;
			}

			var palPath1 = Path.Combine(_altcolorPath, gameName, gameName + ".pal");
			var palPath2 = Path.Combine(_altcolorPath, gameName, "pin2dmd.pal");
			var vniPath1 = Path.Combine(_altcolorPath, gameName, gameName + ".vni");
			var vniPath2 = Path.Combine(_altcolorPath, gameName, "pin2dmd.vni");

			var palPath = File.Exists(palPath1) ? palPath1 : palPath2;
			var vniPath = File.Exists(vniPath1) ? vniPath1 : vniPath2;
			if (File.Exists(palPath)) {
				try {
					Logger.Info("[pin2color] Loading palette file at {0}...", palPath);
					var coloring = new VniColoring(palPath);
					VniAnimationSet vni = null;
					if (File.Exists(vniPath))
					{
						Logger.Info("[pin2color] Loading virtual animation file at {0}...", vniPath);
						vni = new VniAnimationSet(vniPath);
						Logger.Info("[pin2color] Loaded animation set {0}", vni);
						Logger.Info("[pin2color] Animation Dimensions: {0}x{1}", vni.MaxWidth, vni.MaxHeight);
						Analytics.Instance.SetColorizer("VNI/PAL");

					} else {
						Logger.Info("[pin2color] No animation set found");
						Analytics.Instance.SetColorizer("PAL");
					}

					var gray2Colorizer = new Pin2ColorGray2Colorizer(coloring, vni);
					var gray4Colorizer = new Pin2ColorGray4Colorizer(coloring, vni);

					gray2Colorizer.ScalerMode = scalerMode;
					gray4Colorizer.ScalerMode = scalerMode;

					return new Pin2ColorResult {
						Coloring = coloring,
						Gray2Colorizer = gray2Colorizer,
						Gray4Colorizer = gray4Colorizer,
						Vni = vni,
					};

				} catch (Exception e) {
					Logger.Warn(e, "[pin2color] Error initializing: {0}", e.Message);
					Analytics.Instance.ClearColorizer();
				}

			} else {
				Logger.Info("[pin2color] No palette file found at {0}.", palPath);
				Analytics.Instance.ClearColorizer();
			}

			return null;
		}
		
		public ColorizationPlugin LoadPlugin(PluginConfig[] pluginConfigs, bool colorize, string gameName, Color defaultColor, Color[] palette, ScalerMode scalerMode)
		{
			if (_altcolorPath == null || pluginConfigs == null) {
				return null;
			}

			if (pluginConfigs.Length == 0) {
				Logger.Info("[plugin] No colorization plugins configured.");
			}

			// grab the first configured plugin that is active or has passthrough enabled.
			foreach (var config in pluginConfigs) {
				var plugin = new ColorizationPlugin(config, colorize, _altcolorPath, gameName, defaultColor, palette);
				var passthrough = config.PassthroughEnabled && plugin.IsAvailable;
				if (!plugin.IsColoring && !passthrough) {
					continue;
				}

				Logger.Info($"[plugin] Plugin {plugin.GetName()} v{plugin.GetVersion()} loaded.");

				return plugin;
			}
			return null;
		}
	}
}
