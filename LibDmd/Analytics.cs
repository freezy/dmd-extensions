using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net;
using Microsoft.Win32;
using RudderStack;
using NLog;
using OSVersionExtension;
using RudderStack.Model;
using Logger = NLog.Logger;

namespace LibDmd
{
	public static class Analytics
	{

		private static string _id;
		private static RudderOptions _options;
		
		private const string FieldDeviceName = "Device Name"; //Name
		private const string FieldDeviceType = "System Type"; //SystemType
		private const string FieldDeviceMemory = "Total Memory"; //TotalPhysicalMemory
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

		public static void Init(string version)
		{
			_options = new RudderOptions().SetContext(CreateContext(version));
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
			ServicePointManager.Expect100Continue = true;

			RudderStack.Logger.Handlers += LoggingHandler;
			RudderAnalytics.Initialize("2P6989v5ecReLXxEQyVUmSOXR3q", new RudderConfig(dataPlaneUrl: "https://hostsruddahrp.dataplane.rudderstack.com"));
		}

		public static void SourceActive(string source, string gameId)
		{
			RudderAnalytics.Client.Page(GetId(), source, new Dictionary<string, object> { {"Game", gameId} }, _options);
		}

		public static void SourceActive(string source)
		{
			RudderAnalytics.Client.Page(GetId(), source, _options);
		}


		private static RudderContext CreateContext(string version)
		{
			var sysInfo = GetSysInfo();
			var osVer = OSVersion.GetOSVersion();
			return new RudderContext { 
				{ "app", new Dict {
					{ "version", version }
				} },
				{ "device", new Dict {
					{ "name", sysInfo[FieldDeviceName] },
					{ "type", sysInfo[FieldDeviceType] },
					{ "memory_total", sysInfo[FieldDeviceMemory] },
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

		private static Dictionary<string, object> GetSysInfo()
		{
			var info = new Dictionary<string, object>();
			
			using (var searcher = new ManagementObjectSearcher("select * from Win32_Processor")) {
	
				foreach (ManagementObject mo in searcher.Get()) {
					
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
	
				foreach (ManagementObject mo in searcher.Get()) {
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

					// Console.WriteLine($"{mo.Path}\n==========================");
					// foreach(var prop in mo.Properties) {
					// 	Console.WriteLine($"{prop.Name} = {prop.Value} ({prop.Type})");
					// }
				}
			}
			
			using (var searcher = new ManagementObjectSearcher("select * from Win32_ComputerSystem")) {
	
				foreach (ManagementObject mo in searcher.Get()) {

					AddInfo("Name", FieldDeviceName, mo, info);
					AddInfo("SystemType", FieldDeviceType, mo, info);
					AddInfo("TotalPhysicalMemory", FieldDeviceMemory, mo, info);
		
					// Console.WriteLine($"{mo.Path}\n==========================");
					// foreach(var prop in mo.Properties) {
					// 	Console.WriteLine($"{prop.Name} = {prop.Value} ({prop.Type})");
					// }
				}
			}

			return info;
		}
		
		private static void AddInfo(string srcKey, string destKey, ManagementObject mo, Dictionary<string, object> info)
		{
			try {
				PropertyData data = mo.Properties[srcKey];
				if (data.Value == null) {
					return;
				}
				info.Add(destKey, data.Value);
				
			} catch (ManagementException e) {
				
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

		private static string GetId()
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
		
		static void LoggingHandler(RudderStack.Logger.Level level, string message, IDictionary<string, object> args)
		{
			if (args != null) {
				message = args.Keys.Aggregate(message, (current, key) => current + $" {"" + key}: {"" + args[key]},");
			}
			Logger.Info($"[RudderAnalytics] [{level}] {message}");
		}

	}
}
