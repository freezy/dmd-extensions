using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IniParser;
using IniParser.Model;
using LibDmd.Input;
using NLog;

namespace LibDmd.DmdDevice
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
		public readonly GifConfig Gif;
		public string GameName {
			get { return _gameName; }
			set {
				_gameName = value;
				var gameSection = _data.Sections.FirstOrDefault(s => s.SectionName == _gameName);
				GameConfig = gameSection != null ? new GameConfig(gameSection.SectionName, _data, this) : null;
			}
		}
		public GameConfig GameConfig { get; private set; }
		public readonly VpdbConfig VpdbStream;
		public readonly BrowserConfig BrowserStream;
		public readonly PinUpConfig PinUp;

		private readonly string _iniPath;
		private readonly FileIniDataParser _parser;
		private readonly IniData _data;
		private string _gameName;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Configuration()
		{
			var assemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
			_iniPath = Path.Combine(assemblyPath, "DmdDevice.ini");
			_parser = new FileIniDataParser();

			try {
				if (File.Exists(_iniPath)) {
					_data = _parser.ReadFile(_iniPath);
				} else {
					Logger.Warn("No DmdDevice.ini found at {0}, falling back to default values.", _iniPath);
					_data = new IniData();
				}
			} catch (Exception e) {
				Logger.Error(e, "Error parsing .ini file at {0}: {1}", _iniPath, e.Message);
				_data = new IniData();
			}

			Global = new GlobalConfig(_data, this);
			VirtualDmd = new VirtualDmdConfig(_data, this);
			PinDmd1 = new PinDmd1Config(_data, this);
			PinDmd2 = new PinDmd2Config(_data, this);
			PinDmd3 = new PinDmd3Config(_data, this);
			Pin2Dmd = new Pin2DmdConfig(_data, this);
			Video = new VideoConfig(_data, this);
			Gif = new GifConfig(_data, this);
			VpdbStream = new VpdbConfig(_data, this);
			BrowserStream = new BrowserConfig(_data, this);
			PinUp = new PinUpConfig(_data, this);
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

	public class GameConfig : AbstractConfiguration
	{
		public override string Name { get; }
		public GameConfig(string name, IniData data, Configuration parent) : base(data, parent)
		{
			Name = name;
		}
	}

	public class GlobalConfig : AbstractConfiguration
	{
		public override string Name { get; } = "global";
		public ResizeMode Resize => GetEnum("resize", ResizeMode.Fit);
		public bool FlipHorizontally => GetBoolean("fliphorizontally", false);
		public bool FlipVertically => GetBoolean("flipvertically", false);

#if DISABLE_COLORING
		public bool Colorize { get; } = false;
#else
		public bool Colorize => GetBoolean("colorize", true);
#endif
		public GlobalConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd1Config : AbstractConfiguration
	{
		public override string Name { get; } = "pindmd1";
		public bool Enabled => GetBoolean("enabled", true);
		public PinDmd1Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd2Config : AbstractConfiguration
	{
		public override string Name { get; } = "pindmd2";
		public bool Enabled => GetBoolean("enabled", true);
		public PinDmd2Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd3Config : AbstractConfiguration
	{
		public override string Name { get; } = "pindmd3";
		public bool Enabled => GetBoolean("enabled", true);
		public string Port => GetString("port", null);
		public PinDmd3Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class Pin2DmdConfig : AbstractConfiguration
	{
		public override string Name { get; } = "pin2dmd";
		public bool Enabled => GetBoolean("enabled", true);
		public Pin2DmdConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class VirtualDmdConfig : AbstractConfiguration
	{
		public override string Name { get; } = "virtualdmd";

		public bool Enabled => GetBoolean("enabled", true);
		public bool StayOnTop => GetBoolean("stayontop", false);
		public bool IgnoreAr => GetBoolean("ignorear", false);
		public bool UseRegistryPosition => GetBoolean("useregistry", false);
		public bool HideGrip => GetBoolean("hidegrip", false);
		public double Left => GetDouble("left", 0);
		public double Top => GetDouble("top", 0);
		public double Width => GetDouble("width", 1024);
		public double Height => GetDouble("height", 256);
		public double DotSize => GetDouble("dotsize", 1.0);

		public VirtualDmdConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class VideoConfig : AbstractConfiguration
	{
		public override string Name { get; } = "video";
		public bool Enabled => GetBoolean("enabled", false);
		public string Path => GetString("path", ".");
		public VideoConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class GifConfig : VideoConfig
	{
		public override string Name { get; } = "gif";
		public GifConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class BrowserConfig : AbstractConfiguration
	{
		public override string Name { get; } = "browserstream";
		public bool Enabled => GetBoolean("enabled", false);
		public int Port => GetInt("port", 9090);
		public BrowserConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class VpdbConfig : AbstractConfiguration
	{
		public override string Name { get; } = "vpdbstream";
		public bool Enabled => GetBoolean("enabled", false);
		public string EndPoint => GetString("endpoint", "https://api-test.vpdb.io/");
		public VpdbConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinUpConfig : AbstractConfiguration
	{
		public override string Name { get; } = "pinup";
		public bool Enabled => GetBoolean("enabled", false);
		public PinUpConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public abstract class AbstractConfiguration
	{
		public abstract string Name { get; }
		private readonly IniData _data;
		private readonly Configuration _parent;
		private string GameOverridePrefix => Name == "global" ? "" : $"{Name} ";

		protected bool DoWrite = true;
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool HasGameOverride(string key)
		{
			return Name != _parent.GameName && _parent.GameConfig != null && _data[_parent.GameName].ContainsKey(GameOverridePrefix + key);
		}

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

		protected bool GetBoolean(string key, bool fallback)
		{
			if (Name != _parent.GameName && _parent.GameConfig != null && _data[_parent.GameName].ContainsKey(GameOverridePrefix + key)) {
				return _parent.GameConfig.GetBoolean(GameOverridePrefix + key, fallback);
			}

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

		protected int GetInt(string key, int fallback)
		{
			if (Name != _parent.GameName && _parent.GameConfig != null && _data[_parent.GameName].ContainsKey(GameOverridePrefix + key)) {
				return _parent.GameConfig.GetInt(GameOverridePrefix + key, fallback);
			}

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

		protected double GetDouble(string key, double fallback)
		{
			if (Name != _parent.GameName && _parent.GameConfig != null && _data[_parent.GameName].ContainsKey(GameOverridePrefix + key)) {
				return _parent.GameConfig.GetDouble(GameOverridePrefix + key, fallback);
			}

			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}

			try {
				return double.Parse(_data[Name][key]);
			} catch (FormatException) {
				Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be a floating number.");
				return fallback;
			}
		}

		protected string GetString(string key, string fallback)
		{
			if (Name != _parent.GameName && _parent.GameConfig != null && _data[_parent.GameName].ContainsKey(GameOverridePrefix + key)) {
				return _parent.GameConfig.GetString(GameOverridePrefix + key, fallback);
			}

			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}
			return _data[Name][key];
		}

		protected T GetEnum<T>(string key, T fallback)
		{
			if (Name != _parent.GameName && _parent.GameConfig != null && _data[_parent.GameName].ContainsKey(GameOverridePrefix + key)) {
				return _parent.GameConfig.GetEnum(GameOverridePrefix + key, fallback);
			}

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
