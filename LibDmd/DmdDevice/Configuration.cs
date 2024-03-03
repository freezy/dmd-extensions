﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Windows.Media;
using IniParser;
using IniParser.Model;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output.Virtual.AlphaNumeric;
using LibDmd.Output.Virtual.Dmd;
using NLog;
using SkiaSharp;

namespace LibDmd.DmdDevice
{
	public class Configuration : IConfiguration
	{
		public static readonly string EnvConfig = "DMDDEVICE_CONFIG";
		public IGlobalConfig Global { get; private set; }
		public IVirtualDmdConfig VirtualDmd { get; private set; }
		public IVirtualAlphaNumericDisplayConfig VirtualAlphaNumericDisplay { get; private set; }
		public IPinDmd1Config PinDmd1 { get; private set; }
		public IPinDmd2Config PinDmd2 { get; private set; }
		public IPinDmd3Config PinDmd3 { get; private set; }
		public IZeDMDConfig ZeDMD { get; private set; }
		public IZeDMDConfig ZeDMDHD { get; private set; }
		public IZeDMDWiFiConfig ZeDMDWiFi { get; private set; }
		public IZeDMDWiFiConfig ZeDMDHDWiFi { get; private set; }
		public IPin2DmdConfig Pin2Dmd { get; private set; }
		public IPixelcadeConfig Pixelcade { get; private set; }
		public IVideoConfig Video { get; private set; }
		public IGifConfig Gif { get; private set; }
		public IBitmapConfig Bitmap { get; private set; }

		private readonly ISubject<Unit> _onSave = new Subject<Unit>();

		public string GameName {
			get => _gameName;
			set {
				_gameName = value;
				var gameSection = _data.Sections.FirstOrDefault(s => s.SectionName == _gameName);
				GameConfig = gameSection != null ? new GameConfig(_gameName, _data, this) : null;
			}
		}

		public bool HasGameName => _gameName != null;
		public GameConfig GameConfig { get; private set; }
		public IVpdbConfig VpdbStream { get; private set; }
		public IBrowserConfig BrowserStream { get; private set; }
		public INetworkConfig NetworkStream { get; private set; }
		public IPinUpConfig PinUp { get; private set; }
		public IRawOutputConfig RawOutput { get; private set; }
		public string DataPath { get; }

		public void Validate()
		{
			// errors are only logged, and we fall back to defaults.
		}

		private readonly string _iniPath;
		private readonly FileIniDataParser _parser;
		private IniData _data;
		private string _gameName;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private IDisposable _saveSubscription;

		public Configuration(string iniPath = null)
		{
			var envConfigPath = GetEnvConfigPath();
			if (iniPath != null) {
				if (!File.Exists(iniPath)) {
					throw new IniNotFoundException(iniPath);
				}
				_iniPath = iniPath;

			} else if (envConfigPath != null) {
				_iniPath = envConfigPath;

			} else {
				var assemblyPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
				_iniPath = Path.Combine(assemblyPath, "DmdDevice.ini");
			}
			_parser = new FileIniDataParser();
			_parser.Parser.Configuration.AllowDuplicateSections = true;
			_parser.Parser.Configuration.AllowDuplicateKeys = true;

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
			SetupConfig();

			var dataPath = Path.Combine(Path.GetDirectoryName(_iniPath), "dmdext");
			if (Directory.Exists(dataPath)) {
				DataPath = dataPath;
			}
		}

		public void Reload()
		{
			try {
				if (!string.IsNullOrEmpty(_iniPath) && File.Exists(_iniPath)) {
					Logger.Info("Reloading config from {0}.", _iniPath);
					_data = _parser.ReadFile(_iniPath);
					SetupConfig();
				}
			} catch (Exception e) {
				Logger.Error(e, "Error parsing .ini file at {0}: {1}", _iniPath, e.Message);
				_data = new IniData();
			}
		}

		public void SetGameName(string gameName) => GameName = gameName;

		private void SetupConfig()
		{
			Global = new GlobalConfig(_data, this);
			VirtualDmd = new VirtualDmdConfig(_data, this);
			VirtualAlphaNumericDisplay = new VirtualAlphaNumericDisplayConfig(_data, this);
			PinDmd1 = new PinDmd1Config(_data, this);
			PinDmd2 = new PinDmd2Config(_data, this);
			PinDmd3 = new PinDmd3Config(_data, this);
			ZeDMD = new ZeDMDConfig(_data, this);
			ZeDMDHD = new ZeDMDHDConfig(_data, this);
			ZeDMDWiFi = new ZeDMDWiFiConfig(_data, this);
			ZeDMDHDWiFi = new ZeDMDHDWiFiConfig(_data, this);
			Pin2Dmd = new Pin2DmdConfig(_data, this);
			Pixelcade = new PixelcadeConfig(_data, this);
			Video = new VideoConfig(_data, this);
			Gif = new GifConfig(_data, this);
			Bitmap = new BitmapConfig(_data, this);
			VpdbStream = new VpdbConfig(_data, this);
			BrowserStream = new BrowserConfig(_data, this);
			NetworkStream = new NetworkConfig(_data, this);
			PinUp = new PinUpConfig(_data, this);
			RawOutput = new RawOutputConfig(_data, this);

			_saveSubscription?.Dispose();
			_saveSubscription = _onSave.Throttle(TimeSpan.FromMilliseconds(500)).Subscribe(_ => {
				Logger.Info("Saving config to {0}", _iniPath);
				try {
					_parser.WriteFile(_iniPath, _data);

				} catch (Exception e) {
					Logger.Error("Error writing to file: {0}", e.Message);
				}
			});
		}

		public void Save()
		{
			Logger.Info("Scheduling configuration save to {0}", _iniPath);
			_onSave.OnNext(Unit.Default);
		}

		public static string GetEnvConfigPath()
		{
			if (Environment.GetEnvironmentVariable(EnvConfig) == null) {
				return null;
			}
			var paths = Environment.GetEnvironmentVariable(EnvConfig).Split(';');
			foreach (var path in paths) {
				if (File.Exists(path)) {
					return path;
				}
			}
			return null;
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
#endif
		public bool QuitWhenDone { get; set; }
		public int QuitAfter { get; set; } = -1;
		public bool NoClear => false;
		public Color DmdColor => RenderGraph.DefaultColor;
		public ScalerMode ScalerMode {
			get {
				// scaletohd was removed and replaced by ScalerMode.None. This is to keep backwards compatibility.
				var scaleToHd = GetBoolean("scaletohd", false, out var scaleToHdInIni);
				if (scaleToHdInIni) {
					return !scaleToHd ? ScalerMode.None : GetEnum("scalermode", ScalerMode.Scale2x);
				}
				return GetEnum("scalermode", ScalerMode.None);
			}
		}
		public ScalerMode VniScalerMode
		{
			get
			{
				// retrieve from ScalerMode if key not found
				var vniScalerMode = GetEnum("vni.scalermode", ScalerMode.Doubler, out var keyFound);
				if (keyFound) {
					return vniScalerMode;
				}
				var scalerMode = ScalerMode;
				return scalerMode != ScalerMode.None ? scalerMode : ScalerMode.Doubler;
			}
		}

		public string VniKey => GetString("vni.key", null);

		public bool SkipAnalytics => GetBoolean("skipanalytics", false);
		public PluginConfig[] Plugins {
			get {
				var plugins = new List<PluginConfig>();
				var suffix = IntPtr.Size == 8 ? "64" : "";
				for (var i = 0; i < 10; i++) {
					var path = GetString($"plugin.{i}.path{suffix}", null);
					var passthrough = GetBoolean($"plugin.{i}.passthrough", false);
					var scalerMode = GetEnum($"plugin.{i}.scalermode", ScalerMode.Doubler);
					if (path != null) {
						plugins.Add(new PluginConfig(path, passthrough, scalerMode));
					} else {
						break;
					}
				}
				return plugins.ToArray();
			}
		}

		public GlobalConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PluginConfig
	{
		public readonly string Path;
		public readonly bool PassthroughEnabled;
		public readonly ScalerMode ScalerMode;

		public PluginConfig(string path, bool passthroughEnabled, ScalerMode scalerMode)
		{
			Path = path;
			PassthroughEnabled = passthroughEnabled;
			ScalerMode = scalerMode;
		}
	}

	public class PinDmd1Config : AbstractConfiguration, IPinDmd1Config
	{
		public override string Name { get; } = "pindmd1";
		public bool Enabled => GetBoolean("enabled", false);
		public bool AllowHdScaling => GetBoolean("scaletohd", true);
		public PinDmd1Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd2Config : AbstractConfiguration, IPinDmd2Config
	{
		public override string Name { get; } = "pindmd2";
		public bool Enabled => GetBoolean("enabled", false);
		public bool AllowHdScaling => GetBoolean("scaletohd", true);
		public PinDmd2Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PinDmd3Config : AbstractConfiguration, IPinDmd3Config
	{
		public override string Name { get; } = "pindmd3";
		public bool Enabled => GetBoolean("enabled", false);
		public string Port => GetString("port", null);
		public bool AllowHdScaling => GetBoolean("scaletohd", true);
		public PinDmd3Config(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class ZeDMDConfig : AbstractConfiguration, IZeDMDConfig
	{
		public override string Name { get; } = "zedmd";
		public bool Enabled => GetBoolean("enabled", false);
		public bool Debug => GetBoolean("debug", false);
		public int Brightness => GetInt("brightness", -1);
		public int RgbOrder => GetInt("rgborder", -1);
		public string Port => GetString("port", null);
		public bool ScaleRgb24 => GetBoolean("scalergb24", true);
		public bool AllowHdScaling => false;
		public ZeDMDConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class ZeDMDHDConfig : ZeDMDConfig
	{
		public override string Name { get; } = "zedmdhd";
		public new bool AllowHdScaling => true;
		public ZeDMDHDConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class ZeDMDWiFiConfig : ZeDMDConfig, IZeDMDWiFiConfig
	{
		public override string Name { get; } = "zedmdwifi";
		public string WifiAddress => GetString("wifi.address", null);
		public int WifiPort => GetInt("wifi.port", 3333);
		public string WifiSsid => GetString("wifi.ssid", null);
		public string WifiPassword => GetString("wifi.password", null);
		public ZeDMDWiFiConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class ZeDMDHDWiFiConfig : ZeDMDWiFiConfig
	{
		public override string Name { get; } = "zedmdhdwifi";
		public new bool AllowHdScaling => true;
		public ZeDMDHDWiFiConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class Pin2DmdConfig : AbstractConfiguration, IPin2DmdConfig
	{
		public override string Name { get; } = "pin2dmd";
		public bool Enabled => GetBoolean("enabled", false);
		public int Delay => GetInt("delay", 25);
		public bool AllowHdScaling => GetBoolean("scaletohd", true);
		public Pin2DmdConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class PixelcadeConfig : AbstractConfiguration, IPixelcadeConfig
	{
		public override string Name { get; } = "pixelcade";
		public bool Enabled => GetBoolean("enabled", false);
		public string Port => GetString("port", null);
		public ColorMatrix ColorMatrix => GetEnum("matrix", ColorMatrix.Rbg);
		public bool AllowHdScaling => GetBoolean("scaletohd", true);
		public PixelcadeConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class VirtualDmdConfig : AbstractConfiguration, IVirtualDmdConfig
	{
		public override string Name => "virtualdmd";
		public bool Enabled => GetBoolean("enabled", true);
		public bool StayOnTop => GetBoolean("stayontop", false);
		public bool IgnoreAr => GetBoolean("ignorear", false);
		public bool UseRegistryPosition => GetBoolean("useregistry", false);
		public double Left => GetDouble("left", 0);
		public double Top => GetDouble("top", 0);
		public double Width => GetDouble("width", 1024);
		public double Height => GetDouble("height", 256);
		public ScalerMode ScalingMode => GetEnum("scalermode", ScalerMode.Scale2x);

		private readonly Dictionary<string, DmdStyle> _styles = new Dictionary<string, DmdStyle>();

		public string StyleName => GetString("style", "default");

		public DmdStyle Style {
			get {
				var style = GetString("style", "default");
				return _styles.ContainsKey(style) ? _styles[style] : new DmdStyle();
			}
		}

		public VirtualDmdConfig(IniData data, Configuration parent) : base(data, parent)
		{
			if (data[Name] == null)
			{
				return;
			}
			var keyValues = data[Name].GetEnumerator();
			while (keyValues.MoveNext())
			{
				var names = keyValues.Current.KeyName.Split(new[] { '.' }, 4);
				if (names.Length > 1 && names[0] == "style")
				{
					var styleName = names[1];
					var styleProperty = string.Join(".", names.Skip(2).ToArray()); // names[2];
					if (!_styles.ContainsKey(styleName))
					{
						_styles.Add(styleName, new DmdStyle());
					}
					switch (styleProperty)
					{
						case "dotsize":
							_styles[styleName].DotSize = GetDouble(keyValues.Current.KeyName, 1.0);
							break;
						case "dotrounding":
							_styles[styleName].DotRounding = GetDouble(keyValues.Current.KeyName, 0.0);
							break;
						case "dotsharpness":
							_styles[styleName].DotSharpness = GetDouble(keyValues.Current.KeyName, 0.8);
							break;
						case "unlitdot":
							_styles[styleName].UnlitDot = fromSKColor(GetSKColor(keyValues.Current.KeyName, SKColor.Empty));
							break;
						case "brightness":
							_styles[styleName].Brightness = GetDouble(keyValues.Current.KeyName, 1.0);
							break;
						case "dotglow":
							_styles[styleName].DotGlow = GetDouble(keyValues.Current.KeyName, 0.0);
							break;
						case "backglow":
							_styles[styleName].BackGlow = GetDouble(keyValues.Current.KeyName, 0.0);
							break;
						case "gamma":
							_styles[styleName].Gamma = GetDouble(keyValues.Current.KeyName, 1.0);
							break;
						case "tint":
							_styles[styleName].Tint = fromSKColor(GetSKColor(keyValues.Current.KeyName, SKColor.Empty));
							break;
						case "glass":
							var glassTex = GetString(keyValues.Current.KeyName, null);
							_styles[styleName].GlassTexture = glassTex.Equals("null") ? null : GetString(keyValues.Current.KeyName, "null");
							break;
						case "glass.padding.left":
							_styles[styleName].GlassPadding = new System.Windows.Thickness(GetDouble(keyValues.Current.KeyName, 0.0), _styles[styleName].GlassPadding.Top, _styles[styleName].GlassPadding.Right, _styles[styleName].GlassPadding.Bottom);
							break;
						case "glass.padding.top":
							_styles[styleName].GlassPadding = new System.Windows.Thickness(_styles[styleName].GlassPadding.Left, GetDouble(keyValues.Current.KeyName, 0.0), _styles[styleName].GlassPadding.Right, _styles[styleName].GlassPadding.Bottom);
							break;
						case "glass.padding.right":
							_styles[styleName].GlassPadding = new System.Windows.Thickness(_styles[styleName].GlassPadding.Left, _styles[styleName].GlassPadding.Top, GetDouble(keyValues.Current.KeyName, 0.0), _styles[styleName].GlassPadding.Bottom);
							break;
						case "glass.padding.bottom":
							_styles[styleName].GlassPadding = new System.Windows.Thickness(_styles[styleName].GlassPadding.Left, _styles[styleName].GlassPadding.Top, _styles[styleName].GlassPadding.Right, GetDouble(keyValues.Current.KeyName, 0.0));
							break;
						case "glass.color":
							_styles[styleName].GlassColor = fromSKColor(GetSKColor(keyValues.Current.KeyName, SKColor.Empty));
							break;
						case "glass.lighting":
							_styles[styleName].GlassLighting = GetDouble(keyValues.Current.KeyName, 0.0);
							break;
						case "frame":
							var frameTex = GetString(keyValues.Current.KeyName, null);
							_styles[styleName].FrameTexture = frameTex.Equals("null") ? null : GetString(keyValues.Current.KeyName, "null");
							break;
						case "frame.padding.left":
							_styles[styleName].FramePadding = new System.Windows.Thickness(GetDouble(keyValues.Current.KeyName, 0.0), _styles[styleName].FramePadding.Top, _styles[styleName].FramePadding.Right, _styles[styleName].FramePadding.Bottom);
							break;
						case "frame.padding.top":
							_styles[styleName].FramePadding = new System.Windows.Thickness(_styles[styleName].FramePadding.Left, GetDouble(keyValues.Current.KeyName, 0.0), _styles[styleName].FramePadding.Right, _styles[styleName].FramePadding.Bottom);
							break;
						case "frame.padding.right":
							_styles[styleName].FramePadding = new System.Windows.Thickness(_styles[styleName].FramePadding.Left, _styles[styleName].FramePadding.Top, GetDouble(keyValues.Current.KeyName, 0.0), _styles[styleName].FramePadding.Bottom);
							break;
						case "frame.padding.bottom":
							_styles[styleName].FramePadding = new System.Windows.Thickness(_styles[styleName].FramePadding.Left, _styles[styleName].FramePadding.Top, _styles[styleName].FramePadding.Right, GetDouble(keyValues.Current.KeyName, 0.0));
							break;
					}
				}
			}
			//Logger.Info("Parsed styles: {0}", string.Join("\n", _styles.Keys.Select(k => $"{k}: {_styles[k]}")));
		}

		public List<string> GetStyleNames()
		{
			return _styles.Keys.ToList();
		}

		public DmdStyle GetStyle(string name)
		{
			return _styles.ContainsKey(name) ? _styles[name] : null;
		}

		public void ApplyStyle(string name, bool onlyForGame)
		{
			Set("style", name, onlyForGame);
		}

		public void SetStyle(string name, DmdStyle style)
		{
			if (_styles.ContainsKey(name)) {
				_styles.Remove(name);
			}
			_styles.Add(name, style);
			var prefix = "style." + name + ".";
			DoWrite = false;

			Set(prefix + "brightness", style.Brightness);
			Set(prefix + "dotsize", style.DotSize);
			Set(prefix + "dotrounding", style.DotRounding);
			Set(prefix + "dotsharpness", style.DotSharpness);
			Set(prefix + "unlitdot", style.UnlitDot);
			Set(prefix + "dotglow", style.DotGlow);
			Set(prefix + "backglow", style.BackGlow);
			Set(prefix + "gamma", style.Gamma);
			Set(prefix + "tint", new SKColor(style.Tint.R, style.Tint.G, style.Tint.B, style.Tint.A).ToString());
			Set(prefix + "glass", style.GlassTexture ?? "null");
			Set(prefix + "glass.color", new SKColor(style.GlassColor.R, style.GlassColor.G, style.GlassColor.B, style.GlassColor.A).ToString());
			Set(prefix + "glass.lighting", style.GlassLighting);
			Set(prefix + "glass.padding.left", style.GlassPadding.Left);
			Set(prefix + "glass.padding.top", style.GlassPadding.Top);
			Set(prefix + "glass.padding.right", style.GlassPadding.Right);
			Set(prefix + "glass.padding.bottom", style.GlassPadding.Bottom);
			Set(prefix + "frame", style.FrameTexture ?? "null");
			Set(prefix + "frame.padding.left", style.FramePadding.Left);
			Set(prefix + "frame.padding.top", style.FramePadding.Top);
			Set(prefix + "frame.padding.right", style.FramePadding.Right);
			Set(prefix + "frame.padding.bottom", style.FramePadding.Bottom);
			Save();
		}

		public void RemoveStyle(string name)
		{
			if (_styles.ContainsKey(name)) {
				_styles.Remove(name);
			}
			var prefix = "style." + name + ".";
			DoWrite = false;
			Remove("style");
			Remove(prefix + "brightness");
			Remove(prefix + "dotsize");
			Remove(prefix + "dotrounding");
			Remove(prefix + "dotsharpness");
			Remove(prefix + "unlitdot");
			Remove(prefix + "dotglow");
			Remove(prefix + "backglow");
			Remove(prefix + "gamma");
			Remove(prefix + "glass");
			Remove(prefix + "glass.color");
			Remove(prefix + "glass.lighting");
			Remove(prefix + "glass.padding.left");
			Remove(prefix + "glass.padding.top");
			Remove(prefix + "glass.padding.right");
			Remove(prefix + "glass.padding.bottom");
			Remove(prefix + "frame");
			Remove(prefix + "frame.padding.left");
			Remove(prefix + "frame.padding.top");
			Remove(prefix + "frame.padding.right");
			Remove(prefix + "frame.padding.bottom");
			Save();
		}

		private static Color fromSKColor(SKColor skColor)
		{
			return Color.FromArgb(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue);
		}

		public void SetPosition(VirtualDisplayPosition position, bool onlyForGame)
		{
			DoWrite = false;
			Set("left", position.Left, onlyForGame);
			Set("top", position.Top, onlyForGame);
			Set("width", position.Width, onlyForGame);
			Set("height", position.Height, onlyForGame);
			Save();
		}

		public void SetIgnoreAspectRatio(bool ignoreAspectRatio)
		{
			DoWrite = false;
			Set("ignorear", ignoreAspectRatio, false);
			Save();
		}
	}

	public class VirtualAlphaNumericDisplayConfig : AbstractConfiguration, IVirtualAlphaNumericDisplayConfig
	{
		public override string Name { get; } = "alphanumeric";
		public bool Enabled => GetBoolean("enabled", true);
		public bool StayOnTop => GetBoolean("stayontop", false);

		private readonly Dictionary<string, RasterizeStyleDefinition> _styles = new Dictionary<string, RasterizeStyleDefinition>();

		public RasterizeStyleDefinition Style {
			get {
				var style = GetString("style", "default");
				return _styles.ContainsKey(style) ? _styles[style] : new RasterizeStyleDefinition();
			}
		}

		public VirtualAlphaNumericDisplayConfig(IniData data, Configuration parent) : base(data, parent)
		{
			if (data[Name] == null) {
				return;
			}

			var keyValues = data[Name].GetEnumerator();
			while (keyValues.MoveNext()) {
				var names = keyValues.Current.KeyName.Split(new[] { '.' }, 4);

				if (names.Length > 1 && names[0] == "style") {
					var styleName = names[1];
					var styleProperty = names[2];

					if (!_styles.ContainsKey(styleName)) {
						_styles.Add(styleName, new RasterizeStyleDefinition());
					}

					switch (styleProperty) {
						case "skewangle":
							_styles[styleName].SkewAngle = -(float)GetDouble(keyValues.Current.KeyName, 0);
							break;
						case "weight":
							_styles[styleName].SegmentWeight = GetEnum(keyValues.Current.KeyName, SegmentWeight.Thin);
							break;
						case "backgroundcolor":
							_styles[styleName].BackgroundColor = GetSKColor(keyValues.Current.KeyName, _styles[styleName].BackgroundColor);
							break;
						case "foreground":
							ParseLayerStyle(names[3], keyValues.Current, _styles[styleName].Foreground);
							break;
						case "innerglow":
							ParseLayerStyle(names[3], keyValues.Current, _styles[styleName].InnerGlow);
							break;
						case "outerglow":
							ParseLayerStyle(names[3], keyValues.Current, _styles[styleName].OuterGlow);
							break;
						case "background":
							ParseLayerStyle(names[3], keyValues.Current, _styles[styleName].Background);
							break;
					}
				}
			}
			//Logger.Info("Parsed styles: {0}", string.Join("\n", _styles.Keys.Select(k => $"{k}: {_styles[k]}")));
		}

		public List<string> GetStyleNames()
		{
			return _styles.Keys.ToList();
		}

		public RasterizeStyleDefinition GetStyle(string name)
		{
			return _styles[name];
		}

		public VirtualDisplayPosition GetPosition(int displayNumber)
		{
			var prefix = "pos." + displayNumber + ".";
			if (!HasValue(prefix + "height")) {
				return null;
			}
			return new VirtualDisplayPosition(GetDouble(prefix + "left", 0), GetDouble(prefix + "top", 0), 0, GetDouble(prefix + "height", 0));
		}

		public void SetPosition(int display, VirtualDisplayPosition position)
		{
			DoWrite = false;
			var prefix = "pos." + display + ".";
			Set(prefix + "left", position.Left, true);
			Set(prefix + "top", position.Top, true);
			Set(prefix + "height", position.Height, true);
			Save();
		}

		public void ApplyStyle(string name)
		{
			Set("style", name, true);
		}

		public void SetStyle(string name, RasterizeStyleDefinition style)
		{
			if (_styles.ContainsKey(name)) {
				_styles.Remove(name);
			}
			_styles.Add(name, style);
			var prefix = "style." + name + ".";
			DoWrite = false;

			Set(prefix + "skewangle", -style.SkewAngle);
			Set(prefix + "weight", style.SegmentWeight);
			Set(prefix + "backgroundcolor", style.BackgroundColor);
			SetLayerStyle(name, "foreground", style.Foreground);
			SetLayerStyle(name, "innerglow", style.InnerGlow);
			SetLayerStyle(name, "outerglow", style.OuterGlow);
			SetLayerStyle(name, "background", style.Background);
			Save();
		}

		public void RemoveStyle(string name)
		{
			if (_styles.ContainsKey(name)) {
				_styles.Remove(name);
			}
			var prefix = "style." + name + ".";
			DoWrite = false;
			Remove("style");
			Remove(prefix + "skewangle");
			Remove(prefix + "backgroundcolor");
			RemoveLayerStyle(name, "foreground");
			RemoveLayerStyle(name, "innerglow");
			RemoveLayerStyle(name, "outerglow");
			RemoveLayerStyle(name, "background");
			Save();
		}

		private void SetLayerStyle(string styleName, string layerName, RasterizeLayerStyleDefinition layerStyle)
		{
			var prefix = "style." + styleName + "." + layerName + ".";
			Set(prefix + "enabled", layerStyle.IsEnabled);
			if (layerStyle.IsEnabled) {

				Set(prefix + "color", layerStyle.Color);
				Set(prefix + "blur.enabled", layerStyle.IsBlurEnabled);
				if (layerStyle.IsBlurEnabled) {
					Set(prefix + "blur.x", layerStyle.Blur.X);
					Set(prefix + "blur.y", layerStyle.Blur.Y);

				} else {
					Remove(prefix + "blur.x");
					Remove(prefix + "blur.y");
				}

				Set(prefix + "dilate.enabled", layerStyle.IsDilateEnabled);
				if (layerStyle.IsDilateEnabled) {
					Set(prefix + "dilate.x", layerStyle.Dilate.X);
					Set(prefix + "dilate.y", layerStyle.Dilate.Y);

				} else {
					Remove(prefix + "dilate.x");
					Remove(prefix + "dilate.y");
				}

			} else {
				Remove(prefix + "color");
				Remove(prefix + "blur.enabled");
				Remove(prefix + "blur.x");
				Remove(prefix + "blur.y");
				Remove(prefix + "dilate.enabled");
				Remove(prefix + "dilate.x");
				Remove(prefix + "dilate.y");
			}
		}

		private void RemoveLayerStyle(string styleName, string layerName)
		{
			var prefix = "style." + styleName + "." + layerName + ".";
			Remove(prefix + "enabled");
			Remove(prefix + "color");
			Remove(prefix + "blur.enabled");
			Remove(prefix + "blur.x");
			Remove(prefix + "blur.y");
			Remove(prefix + "dilate.enabled");
			Remove(prefix + "dilate.x");
			Remove(prefix + "dilate.y");
		}

		private void ParseLayerStyle(string property, KeyData keyData, RasterizeLayerStyleDefinition style)
		{
			switch (property) {
				case "enabled":
					style.IsEnabled = GetBoolean(keyData.KeyName, false);
					break;
				case "color":
					style.Color = GetSKColor(keyData.KeyName, style.Color);
					break;
				case "blur.enabled":
					style.IsBlurEnabled = GetBoolean(keyData.KeyName, false);
					break;
				case "blur.x":
					style.Blur = new SKPoint(GetInt(keyData.KeyName, 0), style.Blur.Y);
					break;
				case "blur.y":
					style.Blur = new SKPoint(style.Blur.X, GetInt(keyData.KeyName, 0));
					break;
				case "dilate.enabled":
					style.IsDilateEnabled = GetBoolean(keyData.KeyName, false);
					break;
				case "dilate.x":
					style.Dilate = new SKPoint(GetInt(keyData.KeyName, 0), style.Dilate.Y);
					break;
				case "dilate.y":
					style.Dilate = new SKPoint(style.Dilate.X, GetInt(keyData.KeyName, 0));
					break;
			}
		}
	}

	public class VideoConfig : AbstractConfiguration, IVideoConfig
	{
		public override string Name { get; } = "video";
		public bool Enabled => GetBoolean("enabled", false);
		public string Path => GetString("path", ".");
		public bool AllowHdScaling => GetBoolean("scaletohd", true);
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

	public class NetworkConfig : AbstractConfiguration, INetworkConfig
	{
		public override string Name { get; } = "networkstream";
		public bool Enabled => GetBoolean("enabled", false);

		public string Url => GetString("url", "ws://127.0.0.1/server");
		public bool Retry => GetBoolean("retry", false);
		public int RetryInterval => GetInt("retry-interval", 5);

		public NetworkConfig(IniData data, Configuration parent) : base(data, parent)
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
		public PinUpConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public class RawOutputConfig : AbstractConfiguration, IRawOutputConfig
	{
		public override string Name { get; } = "rawoutput";
		public bool Enabled => GetBoolean("enabled", false);
		public RawOutputConfig(IniData data, Configuration parent) : base(data, parent)
		{
		}
	}

	public abstract class AbstractConfiguration
	{
		public abstract string Name { get; }
		private readonly IniData _data;
		private readonly Configuration _parent;
		public bool HasGameName => _parent.HasGameName;
		public string GameName => _parent.GameName;
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
			return GetBoolean(key, fallback, out _);
		}

		protected bool GetBoolean(string key, bool fallback, out bool keyFound)
		{
			if (HasGameSpecificValue(key)) {
				keyFound = true;
				return _parent.GameConfig.GetBoolean(GameOverridePrefix + key, fallback);
			}

			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				keyFound = false;
				return fallback;
			}

			try {
				keyFound = true;
				return bool.Parse(_data[Name][key]);

			} catch (FormatException e) {
				keyFound = true;
				Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be either \"true\" or \"false\".", e);
				return fallback;
			}
		}

		protected int GetInt(string key, int fallback)
		{
			if (HasGameSpecificValue(key)) {
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
			if (HasGameSpecificValue(key)) {
				return _parent.GameConfig.GetDouble(GameOverridePrefix + key, fallback);
			}

			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}

			try {
				var val = double.Parse(_data[Name][key]);
				if (val >= Int32.MaxValue) {
					Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be a smaller than " + Int32.MaxValue + ", falling back to " + fallback + ".");
					return fallback;
				}
				return val;

			} catch (FormatException) {
				Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be a floating number.");
				return fallback;
			}
		}

		protected string GetString(string key, string fallback)
		{
			if (HasGameSpecificValue(key)) {
				return _parent.GameConfig.GetString(GameOverridePrefix + key, fallback);
			}

			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				return fallback;
			}
			return _data[Name][key];
		}

		protected SKColor GetSKColor(string key, SKColor fallback)
		{
			try {
				if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
					return fallback;
				}
				return SKColor.Parse(_data[Name][key]);

			} catch (ArgumentException) {
				Logger.Error("Cannot parse color {0} for {1}, using fallback {2}", _data[Name][key], key, fallback.ToString());
				return fallback;
			}
		}

		protected T GetEnum<T>(string key, T fallback) => GetEnum(key, fallback, out _);

		protected T GetEnum<T>(string key, T fallback, out bool keyFound)
		{
			if (HasGameSpecificValue(key)) {
				keyFound = true;
				return _parent.GameConfig.GetEnum(GameOverridePrefix + key, fallback);
			}

			if (_data[Name] == null || !_data[Name].ContainsKey(key)) {
				keyFound = false;
				return fallback;
			}

			try {
				var e = (T)Enum.Parse(typeof(T), _data[Name][key].Substring(0, 1).ToUpper() + _data[Name][key].Substring(1));
				if (!Enum.IsDefined(typeof(T), e)) {
					throw new ArgumentException();
				}
				keyFound = true;
				return e;

			} catch (ArgumentException) {
				Logger.Error("Value \"" + _data[Name][key] + "\" for \"" + key + "\" under [" + Name + "] must be one of: [ " + string.Join(", ", Enum.GetNames(typeof(T))) + "].");
				keyFound = true;
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

		protected void Set(string key, double value, bool onlyForGame = false)
		{
			if (onlyForGame && CreateGameConfig()) {
				_parent.GameConfig.Set(GameOverridePrefix + key, value);

			} else {
				if (_data[Name] == null) {
					_data.Sections.Add(new SectionData(Name));
				}
				_data[Name][key] = value.ToString(CultureInfo.InvariantCulture);
				if (DoWrite) {
					_parent.Save();
				}
			}
		}

		protected void Set(string key, bool value, bool onlyForGame)
		{
			Set(key, value ? "true" : "false", onlyForGame);
		}

		protected void Set(string key, string value, bool onlyForGame = false)
		{
			if (onlyForGame && CreateGameConfig()) {
				_parent.GameConfig.Set(GameOverridePrefix + key, value);

			} else {
				if (_data[Name] == null) {
					_data.Sections.Add(new SectionData(Name));
				}
				_data[Name][key] = value;
				if (DoWrite) {
					_parent.Save();
				}
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

		protected void Remove(string key)
		{
			if (_data[Name] == null) {
				return;
			}
			if (!_data[Name].ContainsKey(key)) {
				return;
			}
			_data[Name].RemoveKey(key);
			if (DoWrite) {
				_parent.Save();
			}
		}

		private bool HasGameSpecificValue(string key)
		{
			return Name != _parent.GameName
				   && _parent.GameConfig != null
				   && _data[_parent.GameName].ContainsKey(GameOverridePrefix + key);
		}

		protected bool HasValue(string key)
		{
			if (HasGameSpecificValue(key)) {
				return true;
			}
			return _data[Name] != null && _data[Name].ContainsKey(key);
		}

		private bool CreateGameConfig()
		{
			if (string.IsNullOrEmpty(_parent.GameName)) {
				return false;
			}

			if (_parent.GameConfig != null) {
				return true;
			}

			_data.Sections.Add(new SectionData(_parent.GameName));
			_parent.GameName = _parent.GameName; // trigger GameConfig instantiation
			return _parent.GameConfig != null;
		}
	}
}
