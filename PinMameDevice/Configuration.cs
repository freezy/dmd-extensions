using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using IniParser;
using IniParser.Model;
using LibDmd.Input;
using NLog;

namespace PinMameDevice
{
	public class Configuration
	{
		public readonly GlobalConfig Global;
		public readonly VirtualDmdConfig VirtualDmd;
		public readonly PinDmd1Config PinDmd1;
		public readonly PinDmd2Config PinDmd2;
		public readonly PinDmd3Config PinDmd3;
		public readonly Pin2DmdConfig Pin2Dmd;
		public readonly VideoConfig Video;

		private readonly string _iniPath;
		private readonly FileIniDataParser _parser;
		private readonly IniData _data;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Configuration()
		{
			var assemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
			_iniPath = Path.Combine(assemblyPath, "DmdDevice.ini");
			_parser = new FileIniDataParser();
			_data = File.Exists(_iniPath) ? _parser.ReadFile(_iniPath) : new IniData();

			Global = new GlobalConfig(_data, this);
			VirtualDmd = new VirtualDmdConfig(_data, this);
			PinDmd1 = new PinDmd1Config(_data, this);
			PinDmd2 = new PinDmd2Config(_data, this);
			PinDmd3 = new PinDmd3Config(_data, this);
			Pin2Dmd = new Pin2DmdConfig(_data, this);
			Video = new VideoConfig(_data, this);
		}

		public void Save()
		{
			Logger.Info("Saving config to {0}", _iniPath);
			try {
				_parser.WriteFile(_iniPath, _data);

			} catch (UnauthorizedAccessException e) {
				Logger.Error("Error writing to file: {0}", e.Message);
			}
		}
	}

	public class GlobalConfig : AbstractConfiguration
	{
		public override string Name { get; } = "global";

		public ResizeMode Resize
		{
			get { return GetEnum("resize", ResizeMode.Fit); }
			set { Set("resize", value); }
		}

		public bool FlipHorizontally
		{
			get { return GetBoolean("fliphorizontally", false); }
			set { Set("fliphorizontally", value); }
		}

		public bool FlipVertically
		{
			get { return GetBoolean("flipvertically", false); }
			set { Set("flipvertically", value); }
		}

#if DISABLE_COLORING
		public bool Colorize { get; } = false;
#else
		public bool Colorize
		{
			get { return GetBoolean("colorize", true); }
			set { Set("colorize", value); }
		}
#endif
		public GlobalConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd1Config : AbstractConfiguration
	{
		public override string Name { get; } = "pindmd1";

		public bool Enabled
		{
			get { return GetBoolean("enabled", true); }
			set { Set("enabled", value); }
		}

		public PinDmd1Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd2Config : AbstractConfiguration
	{
		public override string Name { get; } = "pindmd2";

		public bool Enabled
		{
			get { return GetBoolean("enabled", true); }
			set { Set("enabled", value); }
		}

		public PinDmd2Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd3Config : AbstractConfiguration
	{
		public override string Name { get; } = "pindmd3";

		public bool Enabled
		{
			get { return GetBoolean("enabled", true); }
			set { Set("enabled", value); }
		}

		public string Port
		{
			get { return GetString("port", null); }
			set { Set("port", value); }
		}

		public PinDmd3Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class Pin2DmdConfig : AbstractConfiguration
	{
		public override string Name { get; } = "pin2dmd";

		public bool Enabled
		{
			get { return GetBoolean("enabled", true); }
			set { Set("enabled", value); }
		}

		public Pin2DmdConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class VirtualDmdConfig : AbstractConfiguration
	{
		public override string Name { get; } = "virtualdmd";

		public bool Enabled
		{
			get { return GetBoolean("enabled", true); }
			set { Set("enabled", value); }
		}

		public bool StayOnTop
		{
			get { return GetBoolean("stayontop", false); }
			set { Set("stayontop", value); }
		}

		public bool IgnoreAr
		{
			get { return GetBoolean("ignorear", false); }
			set { Set("ignorear", value); }
		}

		public bool HideGrip
		{
			get { return GetBoolean("hidegrip", false); }
			set { Set("hidegrip", value); }
		}

		public double Left
		{
			get { return GetDouble("left", 0); }
			set { Set("left", value); }
		}

		public double Top
		{
			get { return GetDouble("top", 0); }
			set { Set("top", value); }
		}

		public double Width
		{
			get { return GetDouble("width", 1024); }
			set { Set("width", value); }
		}

		public double Height
		{
			get { return GetDouble("height", 256); }
			set { Set("height", value); }
		}

		public VirtualDmdConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}

		public void SetPosition(double left, double top, double width, double height)
		{
			DoWrite = false;
			Left = left;
			Top = top;
			Width = width;
			Height = height;
			Save();
		}
	}

	public class VideoConfig : AbstractConfiguration
	{
		public override string Name { get; } = "video";

		public bool Enabled
		{
			get { return GetBoolean("enabled", false); }
			set { Set("enabled", value); }
		}

		public string Path
		{
			get { return GetString("path", "."); }
			set { Set("path", value); }
		}

		public VideoConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}


	public abstract class AbstractConfiguration
	{
		public abstract string Name { get; }
		private readonly IniData _data;
		private readonly Configuration _parent;

		protected bool DoWrite = true;
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected AbstractConfiguration(IniData data, Configuration parent)
		{
			_parent = parent;
			_data = data;
		}

		protected void Save()
		{
			DoWrite = true;
			_parent.Save();
		}
		protected void Set(string key, bool value)
		{
			if (_data[Name] == null) {
				_data.Sections.Add(new SectionData(Name));
			}
			_data[Name][key] = value ? "true" : "false";
			if (DoWrite) {
				_parent.Save();
			}
		}

		protected bool GetBoolean(string key, bool fallback)
		{
			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}

			try {
				return bool.Parse(_data[Name][key]);
			} catch (FormatException e) {
				Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be either \"true\" or \"false\".", e);
				return fallback;
			}
		}

		protected void Set(string key, int value)
		{
			if (_data[Name] == null) {
				_data.Sections.Add(new SectionData(Name));
			}
			_data[Name][key] = value.ToString();
			if (DoWrite) {
				_parent.Save();
			}
		}

		protected int GetInt(string key, int fallback)
		{
			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}

			try {
				return int.Parse(_data[Name][key]);
			} catch (FormatException e) {
				Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be an integer.", e);
				return fallback;
			}
		}

		protected void Set(string key, double value)
		{
			if (_data[Name] == null) {
				_data.Sections.Add(new SectionData(Name));
			}
			_data[Name][key] = value.ToString();
			if (DoWrite) {
				_parent.Save();
			}
		}

		protected double GetDouble(string key, double fallback)
		{
			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}

			try {
				return double.Parse(_data[Name][key]);
			} catch (FormatException e) {
				Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be a floating number.");
				return fallback;
			}
		}

		protected void Set(string key, string value)
		{
			if (_data[Name] == null) {
				_data.Sections.Add(new SectionData(Name));
			}
			_data[Name][key] = value;
			if (DoWrite) {
				_parent.Save();
			}
		}

		protected string GetString(string key, string fallback)
		{
			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}
			return _data[Name][key];
		}

		protected T GetEnum<T>(string key, T fallback)
		{
			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}
			try {
				var e = (T)Enum.Parse(typeof(T), _data[Name][key].Substring(0, 1).ToUpper() + _data[Name][key].Substring(1));
				if (!Enum.IsDefined(typeof(T), e)) {
					throw new ArgumentException();
				}
				return e;
			} catch (ArgumentException) {
				Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be one of: [ " + string.Join(", ", Enum.GetNames(typeof(T))) + "].");
				return fallback;
			}
		}

		protected void Set<T>(string key, T value)
		{
			if (_data[Name] == null) {
				_data.Sections.Add(new SectionData(Name));
			}
			_data[Name][key] = value.ToString();
			if (DoWrite) {
				_parent.Save();
			}
		}
	}
}
