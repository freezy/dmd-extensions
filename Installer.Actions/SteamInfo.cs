using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;

namespace Installer.Actions
{
	public class SteamInfo
	{
		private readonly bool _isSteamInstalled;
		private readonly string _steamInstallPath;
		private readonly List<string> _steamAppPaths;
		private readonly Session _session;

		public SteamInfo(Session session)
		{
			_session = session;
			var prefix = OsUtil.Is64BitOperatingSystem ? @"WOW6432Node\" : "";
			var reg = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\{prefix}Valve\Steam");
			if (reg == null) {
				_session.Log("Could not find Steam.");
				return;
			}

			_isSteamInstalled = true;
			_steamInstallPath = reg.GetValue("InstallPath").ToString();
			_steamAppPaths = GetAppPaths();
		}

		public bool IsGameInstalled(int steamId)
		{
			foreach (var steamAppPath in _steamAppPaths) {
				_session.Log($"Looking into {steamAppPath} for Steam app {steamId}");
				var acfFile = Path.Combine(steamAppPath, $"appmanifest_{steamId}.acf");
				if (File.Exists(acfFile)) {
					_session.Log($"Found Steam app {steamId} at {steamAppPath}!");
					return true;
				}
			}
			_session.Log($"Could not find Steam app {steamId}.");
			return false;
		}

		public string GetInstallationDirectory(int steamId)
		{
			foreach (var steamAppPath in _steamAppPaths) {
				var acfFile = Path.Combine(steamAppPath, $"appmanifest_{steamId}.acf");
				if (File.Exists(acfFile)) {
					return ParseInstallationDirectory(steamAppPath, acfFile);
				}
			}
			return null;
		}

		private List<string> GetAppPaths()
		{
			var appPaths = new List<string>();
			if (!_isSteamInstalled) {
				return appPaths;
			}

			var steamApps = Path.Combine(_steamInstallPath, "steamapps");
			appPaths.Add(steamApps);

			var libraryFoldersSettingFile = Path.Combine(steamApps, "libraryfolders.vdf");
			if (!File.Exists(libraryFoldersSettingFile)) {
				return appPaths;
			}

			var regex = new Regex("\"\\d+\"\\s+\"([^\"]+)");
			var lines = File.ReadAllLines(libraryFoldersSettingFile);
			foreach (var line in lines) {
				var match = regex.Match(line);
				if (match.Success) {
					appPaths.Add(Path.Combine(match.Groups[1].Value, "steamapps"));
				}
			}
			return appPaths;
		}

		private static string ParseInstallationDirectory(string steamAppPath, string acfFile)
		{
			var regex = new Regex("\"installdir\"\\s+\"([^\"]+)");
			var lines = File.ReadAllLines(acfFile);
			foreach (var line in lines) {
				var match = regex.Match(line);
				if (match.Success) {
					return Path.Combine(Path.Combine(steamAppPath, "common"), match.Groups[1].Value);
				}
			}
			return null;
		}
	}
}
