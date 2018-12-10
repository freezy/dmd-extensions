using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using IniParser;
using IniParser.Model;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output.Virtual.AlphaNumeric;
using NLog;
using SkiaSharp;

namespace LibDmd.DmdDevice
{
	public class Configuration : IConfiguration
	{
		public static readonly string EnvConfig = "DMDDEVICE_CONFIG";
		public IGlobalConfig Global { get; }
		public IVirtualDmdConfig VirtualDmd { get; }
		public IVirtualAlphaNumericDisplayConfig VirtualAlphaNumericDisplay { get; }
		public IPinDmd1Config PinDmd1 { get; }
		public IPinDmd2Config PinDmd2 { get; }
		public IPinDmd3Config PinDmd3 { get; }
		public IPin2DmdConfig Pin2Dmd { get; }
		public IVideoConfig Video { get; }
		public IGifConfig Gif { get; }
		public IBitmapConfig Bitmap { get; }

		public string GameName {
			get => _gameName;
			set {
				_gameName = value;
				var gameSection = _data.Sections.FirstOrDefault(s => s.SectionName == _gameName);
				GameConfig = gameSection != null ? new GameConfig(gameSection.SectionName, _data, this) : null;
			}
		}
		public GameConfig GameConfig { get; private set; }
		public IVpdbConfig VpdbStream { get; }
		public IBrowserConfig BrowserStream { get; }
		public IPinUpConfig PinUp { get; }

		private readonly string _iniPath;
		private readonly FileIniDataParser _parser;
		private readonly IniData _data;
		private string _gameName;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Configuration(string iniPath = null)
		{
			if (iniPath != null) {
				if (!File.Exists(iniPath)) {
					throw new IniNotFoundException(iniPath);
				}
				_iniPath = iniPath;

			} else if (Environment.GetEnvironmentVariable(EnvConfig) != null && File.Exists(Environment.GetEnvironmentVariable(EnvConfig))) {
				_iniPath = Environment.GetEnvironmentVariable(EnvConfig);

			} else {
				var assemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
				_iniPath = Path.Combine(assemblyPath, "DmdDevice.ini");
			}
			_parser = new FileIniDataParser();

			try {
				if (File.Exists(_iniPath)) {
					_data = _parser.ReadFile(_iniPath);
					Logger.Info("Successfully loaded config from {0}.", _iniPath);
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
			VirtualAlphaNumericDisplay = new VirtualAlphaNumericDisplayConfig(_data, this);
			PinDmd1 = new PinDmd1Config(_data, this);
			PinDmd2 = new PinDmd2Config(_data, this);
			PinDmd3 = new PinDmd3Config(_data, this);
			Pin2Dmd = new Pin2DmdConfig(_data, this);
			Video = new VideoConfig(_data, this);
			Gif = new GifConfig(_data, this);
			Bitmap = new BitmapConfig(_data, this);
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

	public class GlobalConfig : AbstractConfiguration, IGlobalConfig
	{
		public override string Name { get; } = "global";
		public ResizeMode Resize => GetEnum("resize", ResizeMode.Fit);
		public bool FlipHorizontally => GetBoolean("fliphorizontally", false);
		public bool FlipVertically => GetBoolean("flipvertically", false);

#if DISABLE_COLORING
		public bool Colorize { get; } = false;
#else
		public bool Colorize => GetBoolean("colorize", true);
		public bool QuitWhenDone => false;
		public int QuitAfter => -1;
		public bool NoClear => false;
		public Color DmdColor => RenderGraph.DefaultColor;
#endif
		public GlobalConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd1Config : AbstractConfiguration, IPinDmd1Config
	{
		public override string Name { get; } = "pindmd1";
		public bool Enabled => GetBoolean("enabled", true);
		public PinDmd1Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd2Config : AbstractConfiguration, IPinDmd2Config
	{
		public override string Name { get; } = "pindmd2";
		public bool Enabled => GetBoolean("enabled", true);
		public PinDmd2Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd3Config : AbstractConfiguration, IPinDmd3Config
	{
		public override string Name { get; } = "pindmd3";
		public bool Enabled => GetBoolean("enabled", true);
		public string Port => GetString("port", null);
		public PinDmd3Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class Pin2DmdConfig : AbstractConfiguration, IPin2DmdConfig
	{
		public override string Name { get; } = "pin2dmd";
		public bool Enabled => GetBoolean("enabled", true);
		public int Delay => GetInt("delay", 25);
		public Pin2DmdConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class VirtualDmdConfig : AbstractConfiguration, IVirtualDmdConfig
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

	public class VirtualAlphaNumericDisplayConfig : AbstractConfiguration, IVirtualAlphaNumericDisplayConfig
	{
		public override string Name { get; } = "alphanumeric";

		public bool Enabled => GetBoolean("enabled", true);

		public RasterizeStyleDefinition Style {
			get {
				var style = GetString("style", "default");
				return Styles.ContainsKey(style) ? Styles[style] : new RasterizeStyleDefinition();
			}
		}

		public Dictionary<string, RasterizeStyleDefinition> Styles { get; } = new Dictionary<string, RasterizeStyleDefinition>();

		public VirtualAlphaNumericDisplayConfig(IniData data, Configuration parent) : base(data, parent)
		{
			var keyValues = data[Name].GetEnumerator();
			while (keyValues.MoveNext()) {
				var names = keyValues.Current.KeyName.Split(new []{'.'}, 3);
				if (names.Length > 1 && names[0] == "style") {
					var styleName = names[1];
					var styleProperty = names[2];
					if (!Styles.ContainsKey(styleName)) {
						Styles.Add(styleName, new RasterizeStyleDefinition());
					}
					switch (styleProperty) {
						case "skewangle":
							Styles[styleName].SkewAngle = (float) GetDouble(keyValues.Current.Value, 0);
							break;
						case "backgroundcolor":
							Styles[styleName].BackgroundColor = SKColor.Parse(keyValues.Current.Value);
							break;
						case "foreground.enabled":
							Styles[styleName].Foreground.IsEnabled = GetBoolean(keyValues.Current.Value, false);
							break;
						case "foreground.color":
							Styles[styleName].Foreground.Color = SKColor.Parse(keyValues.Current.Value);
							break;
						case "foreground.blur.enabled":
							Styles[styleName].Foreground.IsBlurEnabled = GetBoolean(keyValues.Current.Value, false);
							break;
						case "foreground.blur.x":
							Styles[styleName].Foreground.Blur = new SKPoint(GetInt(keyValues.Current.Value, 0), Styles[styleName].Foreground.Blur.Y);
							break;
						case "foreground.blur.y":
							Styles[styleName].Foreground.Blur = new SKPoint(Styles[styleName].Foreground.Blur.X, GetInt(keyValues.Current.Value, 0));
							break;
						case "foreground.dilate.enabled":
							Styles[styleName].Foreground.IsDilateEnabled = GetBoolean(keyValues.Current.Value, false);
							break;
						case "foreground.dilate.x":
							Styles[styleName].Foreground.Dilate = new SKPoint(GetInt(keyValues.Current.Value, 0), Styles[styleName].Foreground.Dilate.Y);
							break;
						case "foreground.dilate.y":
							Styles[styleName].Foreground.Dilate = new SKPoint(Styles[styleName].Foreground.Dilate.X, GetInt(keyValues.Current.Value, 0));
							break;
					}
				}
			}
			Logger.Info("Parsed styles: {0}", string.Join("\n", Styles.Keys.Select(k => $"{k}: {Styles[k]}")));
			
		}
	}

	public class VideoConfig : AbstractConfiguration, IVideoConfig
	{
		public override string Name { get; } = "video";
		public bool Enabled => GetBoolean("enabled", false);
		public string Path => GetString("path", ".");
		public VideoConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class GifConfig : VideoConfig, IGifConfig
	{
		public override string Name { get; } = "gif";
		public GifConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class BitmapConfig : AbstractConfiguration, IBitmapConfig
	{
		public override string Name { get; } = "bitmap";
		public bool Enabled => GetBoolean("enabled", false);
		public string Path => GetString("path", ".");
		public BitmapConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class BrowserConfig : AbstractConfiguration, IBrowserConfig
	{
		public override string Name { get; } = "browserstream";
		public bool Enabled => GetBoolean("enabled", false);
		public int Port => GetInt("port", 9090);
		public BrowserConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class VpdbConfig : AbstractConfiguration, IVpdbConfig
	{
		public override string Name { get; } = "vpdbstream";
		public bool Enabled => GetBoolean("enabled", false);
		public string EndPoint => GetString("endpoint", "https://api-test.vpdb.io/");
		public VpdbConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinUpConfig : AbstractConfiguration, IPinUpConfig
	{
		public override string Name { get; } = "pinup";
		public bool Enabled => GetBoolean("enabled", false);
		public string GameName => null;
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
