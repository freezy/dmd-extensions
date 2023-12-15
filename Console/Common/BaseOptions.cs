using System.Windows.Media;
using CommandLine;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Output.Virtual.AlphaNumeric;
using LibDmd.Output.Virtual.Dmd;
using LibDmd.Output.ZeDMD;

namespace DmdExt.Common
{
	internal abstract class BaseOptions : IConfiguration
	{
		[Option('d', "destination", HelpText = "The destination where the DMD data is sent to. One of: [ auto, pindmdv1, pindmdv2, pindmdv3, zedmd, zdmdhd, zedmdwifi, zedmdhdwifi, pin2dmd, virtual, alphanumeric, network ]. Default: \"virtual\".")]
		public DestinationType Destination { get; set; } = DestinationType.Virtual;

		[Option('r', "resize", HelpText = "How the source image is resized. One of: [ stretch, fill, fit ]. Default: \"stretch\".")]
		public ResizeMode Resize { get; set; } = ResizeMode.Stretch;

		[Option("no-virtual", HelpText = "Explicitly disables the virtual DMD when destination is \"auto\". Default: false.")]
		public bool NoVirtualDmd { get; set; } = false;

		[Option("virtual-stay-on-top", HelpText = "Makes the virtual DMD stay on top of other application windows. Default: false.")]
		public bool VirtualDmdOnTop { get; set; } = false;

		[OptionArray("virtual-position", HelpText = "Position and size of virtual DMD. Four values: <Left> <Top> <Width> [<Height>]. Height is optional and can be used for custom aspect ratio. Default: \"0 0 1024\".")]
		public int[] VirtualDmdPosition { get; set; } = { 0, 0, 1024 };

		[Option("virtual-dot-size", HelpText = "Scale the dot size of the virtual DMD. Default: 1")]
		public double VirtualDmdDotSize { get; set; } = 0.92;

		[Option("virtual-dot-rounding", HelpText = "Roundness of the dots of the virtual DMD. 0 = square, 1 = circle. Default: 1")]
		public double VirtualDmdDotRounding { get; set; } = 1;

		[Option("virtual-dot-sharpness", HelpText = "Sharpness of the dots of the virtual DMD. 0 = fuzzy borders, 1 = sharp borders. Default: 0.8")]
		public double VirtualDmdDotSharpness { get; set; } = 0.8;

		[Option("virtual-dot-unlit", HelpText = "Default color for unlit dots of the virtual DMD. Default: #000000")]
		public Color VirtualDmdUnlitDot { get; set; } = Color.FromArgb(0, 0, 0, 0);

		[Option("virtual-brightness", HelpText = "Brightness of the dots of the virtual DMD. Default: 1")]
		public double VirtualDmdBrightness { get; set; } = 0.95;

		[Option("virtual-dot-glow", HelpText = "Glow of the dots of the virtual DMD. Default: 0")]
		public double VirtualDmdDotGlow { get; set; } = 0;

		[Option("virtual-backglow", HelpText = "Glow of the background behind the dots of the virtual DMD. Default: 0")]
		public double VirtualDmdBackGlow { get; set; } = 0;

		[Option("virtual-gamma", HelpText = "Gamma of the virtual DMD. Default: 1")]
		public double VirtualDmdGamma { get; set; } = 1;

		[Option("virtual-tint", HelpText = "Tint color of the virtual DMD. ALpha is the amount of tinting. Default: #00FF5820 for no tinting")]
		public Color VirtualDmdTint { get; set; } = Color.FromArgb(0x00, 0xFF, 0x58, 0x20);

		[Option("virtual-glass-texture", HelpText = "Path to texture for the glass above the dots of the virtual DMD.")]
		public string VirtualDmdGlassTexture { get; set; } = "";

		[OptionArray("virtual-glass-padding", HelpText = "Padding of the glass of the virtual DMD. Four values: <Left> <Top> <Right> <Bottom>. Default: \"0 0 0 0\".")]
		public int[] VirtualDmdGlassPadding { get; set; } = { 0, 0, 0, 0 };

		[Option("virtual-glass-color", HelpText = "Tint of the glass above the dots of the virtual DMD. Default: #00000000")]
		public Color VirtualDmdGlassColor { get; set; } = Color.FromArgb(0, 0, 0, 0);

		[Option("virtual-glass-lighting", HelpText = "Amount of lighting from the DMD that lights the glass. Default: 0")]
		public double VirtualDmdGlassLighting { get; set; } = 0;

		[Option("virtual-frame-texture", HelpText = "Path to the texture for the frame above the dots of the virtual DMD.")]
		public string VirtualDmdFrameTexture { get; set; } = "";

		[OptionArray("virtual-frame-padding", HelpText = "Padding of the frame above the dots of the virtual DMD. Four values: <Left> <Top> <Right> <Bottom>. Default: \"0 0 0 0\".")]
		public int[] VirtualDmdFramePadding { get; set; } = { 0, 0, 0, 0 };

		[Option("scale-to-hd", HelpText = "Scale colorized content up to 256x64, Default: false")]
		public bool ScalingToHD { get; set; } = false;

		[Option("scaler-mode", HelpText = "Scaling mode for colorized content.  [ doubler, scale2x ]. Default: \"scale2x\".")]
		public ScalerMode ScalingMode { get; set; } = ScalerMode.Scale2x;

		[Option('c', "color", HelpText = "Sets the color of a grayscale source that is rendered on an RGB destination. Default: ff3000")]
		public string RenderColor { get; set; } = "ff3000";

		[Option("flip-x", HelpText = "Flips the image horizontally (left/right). Default: false.")]
		public bool FlipHorizontally { get; set; } = false;

		[Option("flip-y", HelpText = "Flips the image vertically (top/down). Default: false.")]
		public bool FlipVertically { get; set; } = false;

		[Option('p', "port", HelpText = "Force COM port for PinDMDv3 devices. Example: \"COM3\".")]
		public string Port { get; set; } = null;

		[Option("output-delay", HelpText = "How long to wait in milliseconds for data to be sent to the device. Default: 25.")]
		public int OutputDelay { get; set; } = 25;

		[Option('q', "quit-when-done", HelpText = "Exit the program when finished, e.g. when Pinball FX or Future Pinball doesn't receive any frames anymore. Default: false")]
		public bool QuitWhenDone { get; set; }

		[Option("quit-after", HelpText = "Exit after n milliseconds. If set to -1, waits indefinitely or until source finishes when -q used. Default: -1")]
		public int QuitAfter { get; set; } = -1;

		[Option("no-clear", HelpText = "Don't clear screen when quitting. Default: false.")]
		public bool NoClear { get; set; } = false;

		[Option('o', "output-to-file", HelpText = "If set, writes all frames as PNG bitmaps to the provided folder.")]
		public string SaveToFile { get; set; }

		[Option("pinup", HelpText = "If set, enable output to PinUP. The value is the default game name.")]
		public string PinUpName { get; set; } = null;

		[Option("dump-frames", HelpText = "If set, dump raw frames into a file.")]
		public bool RawOutputEnabled { get; set; }

		[Option("use-ini", HelpText = "If set, use options from DmdDevice.ini.")]
		public string DmdDeviceIni { get; set; } = null;

		[Option('g', "game", HelpText = "Name of the game running. Useful when retrieving setting from DmdDevice.ini.")]
		public string GameName { get; set; }

		[Option("color-matrix", HelpText = "Color matrix to use for Pixelcade displays. Default: RBG.")]
		public ColorMatrix ColorMatrix { get; set; } = ColorMatrix.Rbg;

		[Option("url", HelpText = "Websocket URL for streaming via network. Default: ws://localhost/server")]
		public string WebsocketUrl { get; set; } = null;

		[Option("retry", HelpText = "If set, retry connecting if the Websocket connection fails. Default: false" )]
		public bool WebsocketRetry { get; set; } = false;
		[Option("retry-interval", HelpText = "In seconds, interval between Websocket connection retry attempts. Default: 5 seconds")]
		public int WebsocketRetryInterval { get; set; } = 5;

		[Option("colorize", HelpText = "Enable frame-by-frame colorization. Default: false")]
		public bool Colorize { get; set; } = false;
		
		[Option("plugin", HelpText = "Path to plugin DLL for frame-by-frame colorization.")]
		public string Plugin { get; set; } = null;

		[Option("plugin-passthrough", HelpText = "If true, frames will always be sent to the plugin, even if no colorization is found. Default: false.")]
		public bool PluginPassthrough { get; set; } = false;

		[Option("skip-analytics", HelpText = "If set, don't send anonymous usage data to the developer. Default: false.")]
		public bool SkipAnalytics { get; set; } = false;

		[Option("--pac-key", HelpText = "Key to decrypt PAC files, in hex.")]
		public string PacKey { get; set; } = null;

		[Option("zedmd-debug", HelpText = "If set, ZeDMD will show its debug informations. Default: false.")]
		public bool Debug { get; set; } = false;

		[Option("zedmd-brightness", HelpText = "Change ZeDMD brightness between 0 and 15.")]
		public int Brightness { get; set; } = -1;

		[Option("zedmd-rgborder", HelpText = "Change ZeDMD RGB order between 0 and 5.")]
		public int RgbOrder { get; set; } = -1;

		[Option("zedmd-wifi-address", HelpText = "Connect to ZeDMD in WiFi mode using this IP address.")]
		public string WifiAddress { get; set; } = null;

		[Option("zedmd-wifi-port", HelpText = "Connect to ZeDMD in WiFi mode using this port. Default: 3333.")]
		public int WifiPort { get; set; } = 3333;

		[Option("zedmd-wifi-ssid", HelpText = "Configure ZeDMD to use this SSID for WiFi mode.")]
		public string WifiSsid { get; set; } = null;

		[Option("zedmd-wifi-port", HelpText = "Configure ZeDMD to use this password for WiFi mode.")]
		public string WifiPassword { get; set; } = null;

		public IGlobalConfig Global { get; }
		public IVirtualDmdConfig VirtualDmd { get; }
		public IVirtualAlphaNumericDisplayConfig VirtualAlphaNumericDisplay { get; }
		public IPinDmd1Config PinDmd1 { get; }
		public IPinDmd2Config PinDmd2 { get; }
		public IPinDmd3Config PinDmd3 { get; }
		public IZeDMDConfig ZeDMD { get; }
		public IZeDMDConfig ZeDMDHD { get; }
		public IZeDMDWiFiConfig ZeDMDWiFi { get; }
		public IZeDMDWiFiConfig ZeDMDHDWiFi { get; }
		public IPin2DmdConfig Pin2Dmd { get; }
		public IPixelcadeConfig Pixelcade { get; }
		public IVideoConfig Video { get; }
		public IGifConfig Gif { get; }
		public IBitmapConfig Bitmap { get; }
		public IVpdbConfig VpdbStream { get; }
		public IBrowserConfig BrowserStream { get; }
		public INetworkConfig NetworkStream { get; }
		public IPinUpConfig PinUp { get; }
		public IRawOutputConfig RawOutput { get; }
		public bool HasGameName => !string.IsNullOrEmpty(GameName);
		public string DataPath => null;

		protected BaseOptions()
		{
			Global = new GlobalConfig(this);
			VirtualDmd = new VirtualDmdOptions(this);
			VirtualAlphaNumericDisplay = new VirtualAlphaNumericDisplayOptions(this);
			PinDmd1 = new PinDmd1Options(this);
			PinDmd2 = new PinDmd2Options(this);
			PinDmd3 = new PinDmd3Options(this);
			ZeDMD = new ZeDMDOptions(this);
			ZeDMDHD = new ZeDMDHDOptions(this);
			ZeDMDWiFi = new ZeDMDWiFiOptions(this);
			ZeDMDHDWiFi = new ZeDMDHDWiFiOptions(this);
			Pin2Dmd = new Pin2DmdOptions(this);
			Pixelcade = new PixelcadeOptions(this);
			Video = new VideoOptions();
			Gif = new GifOptions();
			Bitmap = new BitmapOptions(this);
			VpdbStream = new VpdbOptions();
			BrowserStream = new BrowserOptions();
			NetworkStream = new NetworkOptions(this);
			PinUp = new PinUpOptions(this);
			RawOutput = new RawOutputOptions(this);
		}

		public enum DestinationType
		{
			Auto, PinDMDv1, PinDMDv2, PinDMDv3, zeDMD, zeDMDHD, zeDMDWiFi, zeDMDHDWiFi, PIN2DMD, PIN2DMDXL, PIN2DMDHD, PIXELCADE, Virtual, AlphaNumeric, Network
		}

		public void Validate()
		{
			if (!ColorUtil.IsColor(RenderColor)) {
				throw new InvalidOptionException("Argument --color must be a valid RGB color. Example: \"ff0000\".");
			}

			if (VirtualDmdPosition.Length != 3 && VirtualDmdPosition.Length != 4) {
				throw new InvalidOptionException("Argument --virtual-position must have three or four values: \"<Left> <Top> <Width> [<Height>]\".");
			}

			if (VirtualDmdDotSize <= 0 || VirtualDmdDotSize > 2) {
				throw new InvalidOptionException("Argument --virtual-dotsize must be larger than 0 and smaller than 10.");
			}

			if (VirtualDmdGlassPadding.Length != 4) {
				throw new InvalidOptionException("Argument --virtual-glass-padding must have four values: \"<Left> <Top> <Right> <Bottom>\".");
			}

			if (VirtualDmdFramePadding.Length != 4) {
				throw new InvalidOptionException("Argument --virtual-frame-padding must have four values: \"<Left> <Top> <Right> <Bottom>\".");
			}
		}

		public void Reload()
		{
		}

		public void SetGameName(string gameName) => GameName = gameName;
	}

	internal class GlobalConfig : IGlobalConfig
	{
		private readonly BaseOptions _options;

		public GlobalConfig(BaseOptions options)
		{
			_options = options;
		}

		public ResizeMode Resize => _options.Resize;
		public bool FlipHorizontally => _options.FlipHorizontally;
		public bool FlipVertically => _options.FlipVertically;
		public bool Colorize => _options.Colorize;
		public bool QuitWhenDone
		{
			get => _options.QuitWhenDone;
			set => _options.QuitWhenDone = value;
		}

		public int QuitAfter
		{
			get => _options.QuitAfter;
			set => _options.QuitAfter = value;
		}

		public bool NoClear => _options.NoClear;
		public Color DmdColor => ColorUtil.ParseColor(_options.RenderColor);
		public ScalerMode ScalerMode => _options.ScalingMode;
		public ScalerMode VniScalerMode => _options.ScalingMode;
		public string VniKey => _options.PacKey;
		public bool SkipAnalytics => _options.SkipAnalytics;
		public PluginConfig[] Plugins => _options.Plugin == null
			? new PluginConfig[]{}
			: new []{ new PluginConfig(_options.Plugin, _options.PluginPassthrough, _options.ScalingMode ) };
	}

	internal class VirtualDmdOptions : IVirtualDmdConfig
	{
		private readonly BaseOptions _options;

		public VirtualDmdOptions(BaseOptions options)
		{
			_options = options;
		}

		public DmdStyle Style => new DmdStyle()
		{
			DotSize = _options.VirtualDmdDotSize,
			DotRounding = _options.VirtualDmdDotRounding,
			DotSharpness = _options.VirtualDmdDotSharpness,
			UnlitDot = _options.VirtualDmdUnlitDot,
			Brightness = _options.VirtualDmdBrightness,
			DotGlow = _options.VirtualDmdDotGlow,
			BackGlow = _options.VirtualDmdBackGlow,
			Gamma = _options.VirtualDmdGamma,
			Tint = _options.VirtualDmdTint,
			GlassTexture = _options.VirtualDmdGlassTexture,
			GlassPadding = new System.Windows.Thickness(_options.VirtualDmdGlassPadding[0], _options.VirtualDmdGlassPadding[1], _options.VirtualDmdGlassPadding[2], _options.VirtualDmdGlassPadding[3]),
			GlassColor = _options.VirtualDmdGlassColor,
			GlassLighting = _options.VirtualDmdGlassLighting,
			FrameTexture = _options.VirtualDmdFrameTexture,
			FramePadding = new System.Windows.Thickness(_options.VirtualDmdFramePadding[0], _options.VirtualDmdFramePadding[1], _options.VirtualDmdFramePadding[2], _options.VirtualDmdFramePadding[3])
		};

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto && !_options.NoVirtualDmd
							|| _options.Destination == BaseOptions.DestinationType.Virtual;
		public bool StayOnTop => _options.VirtualDmdOnTop;
		public bool IgnoreAr => _options.VirtualDmdPosition.Length == 4;
		public bool UseRegistryPosition => false;
		public double Left => _options.VirtualDmdPosition[0];
		public double Top => _options.VirtualDmdPosition[1];
		public double Width => _options.VirtualDmdPosition[2];

		public double Height => _options.VirtualDmdPosition.Length == 4
			? _options.VirtualDmdPosition[3]
			: (int) ((double)_options.VirtualDmdPosition[2] / 4);

		public bool HasGameOverride(string key)
		{
			return false;
		}
	}

	internal class VirtualAlphaNumericDisplayOptions : IVirtualAlphaNumericDisplayConfig
	{
		private readonly BaseOptions _options;

		public VirtualAlphaNumericDisplayOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool StayOnTop => _options.VirtualDmdOnTop;
		public bool Enabled => _options.Destination == BaseOptions.DestinationType.AlphaNumeric;
		public RasterizeStyleDefinition Style { get; } = new RasterizeStyleDefinition();
		public void SetPosition(int displayNumber, VirtualDisplayPosition position)
		{
			// we can't write back the position to command line options.
		}
	}

	internal class PinDmd1Options : IPinDmd1Config
	{
		private readonly BaseOptions _options;

		public PinDmd1Options(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
		                       _options.Destination == BaseOptions.DestinationType.PinDMDv1;
	}

	internal class PinDmd2Options : IPinDmd2Config
	{
		private readonly BaseOptions _options;

		public PinDmd2Options(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
		                       _options.Destination == BaseOptions.DestinationType.PinDMDv2;
	}

	internal class PinDmd3Options : IPinDmd3Config
	{
		private readonly BaseOptions _options;

		public PinDmd3Options(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
							   _options.Destination == BaseOptions.DestinationType.PinDMDv3;
		public string Port => _options.Port;
	}

	internal class ZeDMDOptions : IZeDMDConfig
	{
		private readonly BaseOptions _options;

		public ZeDMDOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
							   _options.Destination == BaseOptions.DestinationType.zeDMD;
		public bool Debug => _options.Debug;
		public int Brightness => _options.Brightness;
		public int RgbOrder => _options.RgbOrder;
		public string Port => _options.Port;
	}

	internal class ZeDMDHDOptions : IZeDMDConfig
	{
		private readonly BaseOptions _options;

		public ZeDMDHDOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
							   _options.Destination == BaseOptions.DestinationType.zeDMDHD;
		public bool Debug => _options.Debug;
		public int Brightness => _options.Brightness;
		public int RgbOrder => _options.RgbOrder;
		public string Port => _options.Port;
	}

	internal class ZeDMDWiFiOptions : IZeDMDWiFiConfig
	{
		private readonly BaseOptions _options;

		public ZeDMDWiFiOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
							   _options.Destination == BaseOptions.DestinationType.zeDMDWiFi;
		public bool Debug => _options.Debug;
		public int Brightness => _options.Brightness;
		public int RgbOrder => _options.RgbOrder;
		public string Port => _options.Port;
		public string WifiAddress => _options.WifiAddress;
		public int WifiPort => _options.WifiPort;
		public string WifiSsid => _options.WifiSsid;
		public string WifiPassword => _options.WifiPassword;
	}

	internal class ZeDMDHDWiFiOptions : IZeDMDWiFiConfig
	{
		private readonly BaseOptions _options;

		public ZeDMDHDWiFiOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
							   _options.Destination == BaseOptions.DestinationType.zeDMDHDWiFi;
		public bool Debug => _options.Debug;
		public int Brightness => _options.Brightness;
		public int RgbOrder => _options.RgbOrder;
		public string Port => _options.Port;
		public string WifiAddress => _options.WifiAddress;
		public int WifiPort => _options.WifiPort;
		public string WifiSsid => _options.WifiSsid;
		public string WifiPassword => _options.WifiPassword;
	}

	internal class Pin2DmdOptions : IPin2DmdConfig
	{
		private readonly BaseOptions _options;

		public Pin2DmdOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
		                       _options.Destination == BaseOptions.DestinationType.PIN2DMD;

		public int Delay => _options.OutputDelay;
	}
	
	internal class PixelcadeOptions : IPixelcadeConfig
	{
		private readonly BaseOptions _options;

		public PixelcadeOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Auto ||
		                       _options.Destination == BaseOptions.DestinationType.PIXELCADE;
		public string Port => _options.Port;
		public ColorMatrix ColorMatrix => _options.ColorMatrix;
	}

	internal class VideoOptions : IVideoConfig
	{
		public bool Enabled => false;
		public string Path => null;
	}

	internal class GifOptions : IGifConfig
	{
		public bool Enabled => false;
		public string Path => null;
	}

	internal class BitmapOptions : IBitmapConfig
	{
		private readonly BaseOptions _options;

		public BitmapOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.SaveToFile != null;
		public string Path => _options.SaveToFile;
	}

	internal class VpdbOptions : IVpdbConfig
	{
		public bool Enabled => false;
		public string EndPoint => null;
	}

	internal class BrowserOptions : IBrowserConfig
	{
		public bool Enabled => false;
		public int Port => 0;
	}
	
	internal class NetworkOptions : INetworkConfig
	{
		private readonly BaseOptions _options;

		public NetworkOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.Destination == BaseOptions.DestinationType.Network;
		public string Url => _options.WebsocketUrl;
		public bool Retry => _options.WebsocketRetry;
		public int RetryInterval => _options.WebsocketRetryInterval;
	}

	internal class PinUpOptions : IPinUpConfig
	{
		private readonly BaseOptions _options;

		public PinUpOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.PinUpName != null;
		public string GameName => _options.PinUpName;
	}

	internal class RawOutputOptions : IRawOutputConfig
	{
		private readonly BaseOptions _options;

		public RawOutputOptions(BaseOptions options)
		{
			_options = options;
		}

		public bool Enabled => _options.RawOutputEnabled;
	}
}
