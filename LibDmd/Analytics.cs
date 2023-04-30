using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net;
using LibDmd.Output;
using Microsoft.Win32;
using RudderStack;
using NLog;
using OSVersionExtension;
using RudderStack.Model;
using Logger = NLog.Logger;

namespace LibDmd
{
	public class Analytics
	{
		private string _id;
		private RudderOptions _options;
		private readonly Dict _data = new Dict();
		private readonly HashSet<string> _displays = new HashSet<string>();
		
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

		public void Init(string version, string runner)
		{
			_options = new RudderOptions().SetContext(CreateContext(version, runner));
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
			ServicePointManager.Expect100Continue = true;

			RudderStack.Logger.Handlers += LoggingHandler;
			RudderAnalytics.Initialize("2P6989v5ecReLXxEQyVUmSOXR3q", new RudderConfig(dataPlaneUrl: "https://hostsruddahrp.dataplane.rudderstack.com"));
		}

		public void Send()
		{
			_data["Weight"] = 1 / _displays.Count;
			foreach (var display in _displays) {
				_data["Display"] = display;
				RudderAnalytics.Client.Track(GetId(), "Game Started", _data, _options);
			}
		}

		public void SetSource(string source, string gameId)
		{
			_data["Host"] = source;
			_data["Game"] = gameId;

		}

		public void SetSource(string host)
		{
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

		private RudderContext CreateContext(string version, string runner)
		{
			var sysInfo = GetSysInfo();
			var osVer = OSVersion.GetOSVersion();
			return new RudderContext { 
				{ "app", new Dict {
					{ "version", version },
					{ "runner", runner },
					{ "distributor", Distributor }
				} },
				{ "device", new Dict {
					{ "name", sysInfo[FieldDeviceName] },
					{ "type", sysInfo[FieldDeviceType] },
					{ "memory_total", $"{Math.Round(((UInt64)sysInfo[FieldDeviceMemory]) / 1073741824d)}GB" },
					{ "cpu_model", sysInfo[FieldCpuName] },
					{ "cpu_clock_speed", sysInfo[FieldCpuClockSpeed] },
					{ "cpu_manufacturer", sysInfo[FieldCpuManufacturer] },
					{ "cpu_num_cores", sysInfo[FieldCpuNumCores] },
					{ "cpu_num_processors", sysInfo[FieldCpuNumProcessors] },
					{ "cpu_id", sysInfo[FieldCpuId] },
					{ "gpu_model", sysInfo[FieldGpuName] },
					{ "gpu_manufacturer", sysInfo[FieldGpuManufacturer] },
					{ "gpu_driver_version", sysInfo[FieldGpuDriverVersion] },
					{ "gpu_driver_date", ParseDate(sysInfo[FieldGpuDriverDate].ToString()) },
				} },
				{ "locale", CultureInfo.InstalledUICulture.Name },
				{ "timezone", TimeZoneConverter.TZConvert.WindowsToIana(TimeZone.CurrentTimeZone.StandardName) },
				{ "os", new Dict {
					{ "name", OSVersion.GetOperatingSystem().ToString() },
					{ "version", $"{osVer.Version.Major}.{osVer.Version.Minor}.{osVer.Version.Build}" }
				} },
				{ "screen", new Dict {
					{ "width", sysInfo[FieldGpuResWidth] },
					{ "height", sysInfo[FieldGpuResHeight] }
				} }
			};
		}

		private Dictionary<string, object> GetSysInfo()
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
				info.Add(destKey, data.Value);
				
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
		
		void LoggingHandler(RudderStack.Logger.Level level, string message, IDictionary<string, object> args)
		{
			if (args != null) {
				message = args.Keys.Aggregate(message, (current, key) => current + $" {"" + key}: {"" + args[key]},");
			}
			Logger.Info($"[RudderAnalytics] [{level}] {message}");
		}
		
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
