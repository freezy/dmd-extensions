using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net;
using System.Threading;
using LibDmd.Output;
using Microsoft.Win32;
using RudderStack;
using NLog;
using OSVersionExtension;
using RudderStack.Model;
using WebSocketSharp;
using Logger = NLog.Logger;

namespace LibDmd
{
	public class Analytics
	{
		private string _id;
		private RudderOptions _options;
		private readonly Dict _data = new Dict();
		private readonly HashSet<string> _displays = new HashSet<string>();
		private DateTime _gameStartedAt;
		private bool _hasGameStarted;
		private bool _isDisabled;
		
		private const string FieldDeviceName = "Device Name";
		private const string FieldDeviceType = "System Type";
		private const string FieldDeviceMemory = "Total Memory";
		private const string FieldCpuName = "CPU Name";
		private const string FieldCpuClockSpeed = "CPU Clock Speed"; 
		private const string FieldCpuManufacturer = "CPU Manufacturer";
		private const string FieldCpuNumCores = "CPU Cores";
		private const string FieldCpuNumProcessors = "CPU Logical Processors";
		private const string FieldCpuId = "CPU ID";
		private const string FieldGpuName = "GPU Name";
		private const string FieldGpuResWidth = "Screen Width";
		private const string FieldGpuResHeight = "Screen Height";
		private const string FieldGpuManufacturer = "GPU Manufacturer";
		private const string FieldGpuDriverVersion = "GPU Driver Version";
		private const string FieldGpuDriverDate = "GPU Driver Date";
		
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static Analytics _instance;
		
		public static Analytics Instance => _instance ?? (_instance = new Analytics());

		public void Disable(bool log = true)
		{
			_isDisabled = true;
			if (log) {
				Logger.Info("Analytics disabled.");
			}
		}

		public void Init(string version, string runner)
		{
			_options = new RudderOptions().SetContext(CreateContext(version, runner));
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
			ServicePointManager.Expect100Continue = true;

			if (_isDisabled) {
				return;
			}
#if DEBUG			
			//RudderStack.Logger.Handlers += LoggingHandler;
#endif
			RudderAnalytics.Initialize("2P6989v5ecReLXxEQyVUmSOXR3q", new RudderConfig(dataPlaneUrl: "https://hostsruddahrp.dataplane.rudderstack.com"));
		}

		public void StartGame()
		{
			if (_isDisabled) {
				return;
			}
			_data["Weight"] = 1 / _displays.Count;
			foreach (var display in _displays) {
				_data["Display"] = display;
				try {
					RudderAnalytics.Client.Track(GetId(), "Game Started", _data, _options);
				} catch (Exception) {
					// do nothing
				}
			}
		}

		public void SetSource(string source, string gameId)
		{
			try {
				StartTimer();
			} catch (Exception e) {
				Logger.Warn(e, "Failed to start timer.");
			}
			_data["Host"] = source;
			_data["Game"] = gameId;
		}

		public void SetSource(string host)
		{
			try {
				StartTimer();
			} catch (Exception e) {
				Logger.Warn(e, "Failed to start timer.");
			}
			_data["Host"] = host;
			if (_data.ContainsKey("Game")) {
				_data.Remove("Game");
			}
		}

		public void ClearSource()
		{
			if (_data.ContainsKey("Game")) {
				_data.Remove("Game");
			}
			if (_data.ContainsKey("Host")) {
				_data.Remove("Host");
			}
		}

		public void AddDestination(IDestination dest)
		{
			_displays.Add(dest.Name);
		}

		public void ClearVirtualDestinations()
		{
			_displays.RemoveWhere(d => d.Contains("Virtual"));
		}
		
		public void SetColorizer(string name)
		{
			_data["Colorizer"] = name;
		}

		public void ClearColorizer()
		{
			if (_data.ContainsKey("Colorizer")) {
				_data.Remove("Colorizer");
			}
		}
		
		public void EndGame()
		{
			if (_isDisabled || !_hasGameStarted) {
				return;
			}
			var duration = Math.Round((DateTime.Now - _gameStartedAt).TotalSeconds);
			_data["Duration"] = duration;
			_data["Weight"] = 1 / (_displays.Count == 0 ? 1 : _displays.Count);
			try {
				foreach (var display in _displays) {
					_data["Display"] = display;
					RudderAnalytics.Client.Track(GetId(), "Game Ended", _data, _options);
				}
				_hasGameStarted = false;
				// this used to block, now on a separate thread it doesn't seem to anymore.
				new Thread(() => RudderAnalytics.Client.Flush()).Start();

			} catch (Exception) {
				// do nothing
			}
		}
		
		private void StartTimer()
		{
			if (_hasGameStarted) {
				EndGame();
			}
			_hasGameStarted = true;
			_gameStartedAt = DateTime.Now;
			if (_data.ContainsKey("Duration")) {
				_data.Remove("Duration");
			}
		}

		private RudderContext CreateContext(string version, string runner)
		{
			if (_isDisabled) {
				return new RudderContext();
			}

			var sysInfo = GetSysInfo();
			
			var os = OSVersion.GetOSVersion();
			var osVersion = string.Empty;
			var osBuild = 0;
			var buildFromRegistry = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber", null);
			if (buildFromRegistry != null) {
				if (Int32.TryParse(buildFromRegistry.ToString(), out osBuild) && osBuild >= 22000) {
					osVersion = "Windows 11";
				}
			}
			if (osVersion.IsNullOrEmpty()) {
				osVersion = OSVersion.GetOperatingSystem().ToString();
			}
			
			return new RudderContext {
				{ "app", new Dict {
					{ "version", version },
					{ "runner", runner },
					{ "distributor", Distributor }
				} },
				{ "device", new Dict {
					{ "type", SysInfo(sysInfo, FieldDeviceType) },
					{ "memory_total", $"{Math.Round(((UInt64)SysInfo(sysInfo, FieldDeviceMemory)) / 1073741824d)}GB" },
					{ "cpu_model", SysInfo(sysInfo, FieldCpuName) },
					{ "cpu_clock_speed", SysInfo(sysInfo, FieldCpuClockSpeed) },
					{ "cpu_manufacturer", SysInfo(sysInfo, FieldCpuManufacturer) },
					{ "cpu_num_cores", SysInfo(sysInfo, FieldCpuNumCores) },
					{ "cpu_num_processors", SysInfo(sysInfo, FieldCpuNumProcessors) },
					{ "gpu_model", SysInfo(sysInfo, FieldGpuName) },
					{ "gpu_manufacturer", SysInfo(sysInfo, FieldGpuManufacturer) },
					{ "gpu_driver_version", SysInfo(sysInfo, FieldGpuDriverVersion) },
					{ "gpu_driver_date", ParseDate(SysInfo(sysInfo, FieldGpuDriverDate)?.ToString()) },
				} },
				{ "locale", CultureInfo.InstalledUICulture.Name },
				{ "os", new Dict {
					{ "name", osVersion },
					{ "version", $"{os.Version.Major}.{os.Version.Minor}.{os.Version.Build}" },
					{ "build", osBuild }
				} },
				{ "screen", new Dict {
					{ "width", SysInfo(sysInfo, FieldGpuResWidth) },
					{ "height", SysInfo(sysInfo, FieldGpuResHeight) }
				} }
			};
		}

		private static object SysInfo(IReadOnlyDictionary<string, object> sysInfo, string key)
		{
			return sysInfo.TryGetValue(key, out var value) ? value : null;
		}

		private static Dictionary<string, object> GetSysInfo()
		{
			var info = new Dictionary<string, object>();
			
			using (var searcher = new ManagementObjectSearcher("select * from Win32_Processor")) {
				foreach (var obj in searcher.Get()) {
					if (!(obj is ManagementObject mo)) {
						continue;
					}
					AddInfo("Name", FieldCpuName, mo, info);
					AddInfo("CurrentClockSpeed", FieldCpuClockSpeed, mo, info);
					AddInfo("Manufacturer", FieldCpuManufacturer, mo, info);
					AddInfo("NumberOfCores", FieldCpuNumCores, mo, info);
					AddInfo("NumberOfLogicalProcessors", FieldCpuNumProcessors, mo, info);
					AddInfo("ProcessorId", FieldCpuId, mo, info);
					
					// Console.WriteLine($"{mo.Path}\n==========================");
					// foreach(var prop in mo.Properties) {
					// 	Console.WriteLine($"{prop.Name} = {prop.Value} ({prop.Type})");
					// }
				}
			}
			using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController")) {
				foreach (var obj in searcher.Get()) {
					if (!(obj is ManagementObject mo)) {
						continue;
					}
					PropertyData currentBitsPerPixel = mo.Properties["CurrentBitsPerPixel"];
					PropertyData maxRefreshRate = mo.Properties["MaxRefreshRate"];
					PropertyData description = mo.Properties["Description"];
					if (currentBitsPerPixel.Value == null || maxRefreshRate.Value == null || (UInt32)maxRefreshRate.Value == 0 || description.Value == null) {
						continue;
					}
					AddInfo("Description", FieldGpuName, mo, info);
					AddInfo("CurrentHorizontalResolution", FieldGpuResWidth, mo, info);
					AddInfo("CurrentVerticalResolution", FieldGpuResHeight, mo, info);
					AddInfo("AdapterCompatibility", FieldGpuManufacturer, mo, info);
					AddInfo("DriverVersion", FieldGpuDriverVersion, mo, info);
					AddInfo("DriverDate", FieldGpuDriverDate, mo, info);
				}
			}
			using (var searcher = new ManagementObjectSearcher("select * from Win32_ComputerSystem")) {
				foreach (var obj in searcher.Get()) {
					if (!(obj is ManagementObject mo)) {
						continue;
					}
					AddInfo("Name", FieldDeviceName, mo, info);
					AddInfo("SystemType", FieldDeviceType, mo, info);
					AddInfo("TotalPhysicalMemory", FieldDeviceMemory, mo, info);
				}
			}

			return info;
		}
		
		private static void AddInfo(string srcKey, string destKey, ManagementBaseObject mo, IDictionary<string, object> info)
		{
			try {
				PropertyData data = mo.Properties[srcKey];
				if (data.Value == null) {
					return;
				}
				info[destKey] = data.Value;
				
			} catch (ManagementException e) {
				Logger.Warn($"Cannot get {srcKey} from WMI: {e.Message}", e);
			}
		}

		private static string ParseDate(string str)
		{
			try {
				return ManagementDateTimeConverter.ToDateTime(str).ToString("o");
			} catch {
				return str;
			}
		}

		private string GetId()
		{
			if (_id != null) {
				return _id;
			}

			const string regPath = @"SOFTWARE\DMD Extensions";
			const string regId = "Id";

			var key = Registry.CurrentUser.OpenSubKey(regPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
			if (key == null) {
				key = Registry.CurrentUser.CreateSubKey(regPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
				if (key == null) {
					return "00000000-0000-0000-0000-000000000000";
				}
			}
			_id = (string)key.GetValue(regId);
			
			if (_id != null) {
				return _id;
			}
			
			_id = Guid.NewGuid().ToString();
			key.SetValue(regId, _id, RegistryValueKind.String);
			key.Close();
			return _id;
		}

#if DEBUG			
		// ReSharper disable once UnusedMember.Local
		static void LoggingHandler(RudderStack.Logger.Level level, string message, IDictionary<string, object> args)
		{
			if (args != null) {
				message = args.Keys.Aggregate(message, (current, key) => current + $" {"" + key}: {"" + args[key]},");
			}
			Logger.Info($"[RudderAnalytics] [{level}] {message}");
		}
#endif
		
#if SRC_GITHUB
		private const string Distributor = "GitHub";
#elif SRC_PIXELCADE
		private const string Distributor = "Pixelcade";
#elif SRC_BALLER
		private const string Distributor = "Baller Installer";
#elif SRC_VPINBALL
		private const string Distributor = "VPX Installer";
#else
		private const string Distributor = "Homebrew";
#endif

	}
}
