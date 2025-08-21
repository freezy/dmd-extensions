using System;
using System.IO;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Plugin;
using LibDmd.DmdDevice;
using NLog;

namespace LibDmd.Converter.Vni
{
	public class ColorizationLoader
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly string _altcolorPath;

		public ColorizationLoader()
		{
			_altcolorPath = PathUtil.GetVpmFolder("altcolor", "[serum]");
		}

		public AbstractConverter LoadSerum(string gameName, ScalerMode scalerMode)
		{
			if (_altcolorPath == null) {
				return null;
			}

			var altColorDir = new DirectoryInfo(Path.Combine(_altcolorPath, gameName));
			var serumFileCRZ = PathUtil.GetLastCreatedFile(altColorDir, "cRZ");
			var serumFileCROM = PathUtil.GetLastCreatedFile(altColorDir, "cROM");
			var serumFileCROMC = PathUtil.GetLastCreatedFile(altColorDir, "cROMc");
			if (serumFileCRZ != null || serumFileCROM != null || serumFileCROMC != null) {
				var serumPath = serumFileCROMC != null ? serumFileCROMC.FullName : (serumFileCROM != null ? serumFileCROM.FullName : serumFileCRZ.FullName);
				try {
					var serum = new Serum.Serum(_altcolorPath, gameName, scalerMode);
					if (serum.IsLoaded) {
						Logger.Info($"[serum] Serum colorizer v{Serum.Serum.GetVersion()} initialized.");
						Logger.Info($"[serum] Loaded {serum.ColorizationVersion} colorization at {serumPath}.");
						Analytics.Instance.SetColorizer("Serum");
						return serum;
					}
					Logger.Warn($"[serum] Found Serum coloring file at {serumPath}, but could not load colorizer.");

				} catch (Exception e) {
					Logger.Warn(e, "[serum] Error initializing colorizer: {0}", e.Message);
					Analytics.Instance.ClearColorizer();
				}
			} else {
				Logger.Info($"[serum] No colorization found at {altColorDir.FullName}...");
				Analytics.Instance.ClearColorizer();
			}

			return null;
		}

		public AbstractConverter LoadVniColorizer(string gameName, ScalerMode scalerMode, string vniKey)
		{
			if (_altcolorPath == null) {
				Analytics.Instance.ClearColorizer();
				return null;
			}

			var loader = new VniLoader(_altcolorPath, gameName);
			if (!loader.FilesExist) {
				Logger.Info("No palette file found at {0}.", Path.Combine(_altcolorPath, gameName));
				return null;
			}

			try {
				loader.Load(vniKey);
				return new VniColorizer(loader.Pal, loader.Vni) { ScalerMode = scalerMode };

			} catch (Exception e) {
				Logger.Warn(e, "Error initializing colorizer: {0}", e.Message);
				return null;
			}
		}

		public AbstractConverter LoadPlugin(PluginConfig[] pluginConfigs, bool colorize, string gameName, Color defaultColor, Color[] palette)
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
				if (!plugin.CanColorize && !passthrough) {
					continue;
				}

				Logger.Info($"[plugin] Plugin {plugin.GetName()} v{plugin.GetVersion()} loaded.");
				Analytics.Instance.SetColorizer($"PLUGIN ({Path.GetFileName(config.Path)})");

				return plugin;
			}
			return null;
		}
	}
}

