using System.Windows.Media;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Input;
using LibDmd.Output.Virtual.AlphaNumeric;
using LibDmd.Output.Virtual.Dmd;

namespace LibDmd.Test
{
	public class TestConfiguration : IConfiguration
	{
		public IGlobalConfig Global { get; set; } = new TestGlobalConfig();
		public IVirtualDmdConfig VirtualDmd { get; set; } = new TestVirtualDmdConfig();
		public IVirtualAlphaNumericDisplayConfig VirtualAlphaNumericDisplay { get; set; } = new TestVirtualAlphaNumericDisplayConfig();
		public IPinDmd1Config PinDmd1 { get; set; } = new TestPinDmd1Config();
		public IPinDmd2Config PinDmd2 { get; set; } = new TestPinDmd2Config();
		public IPinDmd3Config PinDmd3 { get; set; } = new TestPinDmd3Config();
		public IZeDMDConfig ZeDMD { get; set; } = new TestZeDMDConfig();
		public IPin2DmdConfig Pin2Dmd { get; set; } = new TestPin2DmdConfig();
		public IPixelcadeConfig Pixelcade { get; set; } = new TestPixelcadeConfig();
		public IVideoConfig Video { get; set; } = new TestVideoConfig();
		public IGifConfig Gif { get; set; } = new TestGifConfig();
		public IBitmapConfig Bitmap { get; set; } = new TestBitmapConfig();
		public IVpdbConfig VpdbStream { get; set; } = new TestVpdbConfig();
		public IBrowserConfig BrowserStream { get; set; } = new TestBrowserConfig();
		public INetworkConfig NetworkStream { get; set; } = new TestNetworkConfig();
		public IPinUpConfig PinUp { get; set; } = new TestPinUpConfig();
		public IRawOutputConfig RawOutput { get; set; } = new RawOutputConfig();
		public bool HasGameName => !string.IsNullOrEmpty(GameName);
		public string DataPath { get; set; }

		public string GameName { get; set; }

		public void Validate()
		{
		}

		public void Reload()
		{
		}

		public void SetGameName(string gameName)
		{
			GameName = gameName;
		}
	}

	public class TestGlobalConfig : IGlobalConfig
	{
		public ResizeMode Resize { get; set; }
		public bool FlipHorizontally { get; set; }
		public bool FlipVertically { get; set; }
		public bool Colorize { get; set; }
		public bool QuitWhenDone { get; set; }
		public int QuitAfter { get; set; }
		public bool NoClear { get; set; }
		public Color DmdColor { get; set; }
		public ScalerMode ScalerMode { get; set; }
		public ScalerMode VniScalerMode { get; set; }
		public string VniKey { get; set; }
		public bool SkipAnalytics => true;
		public PluginConfig[] Plugins { get; set; }
	}

	public class TestVirtualDmdConfig : IVirtualDmdConfig
	{
		public bool Enabled { get; set; }
		public bool StayOnTop { get; set; }
		public bool IgnoreAr { get; set; }
		public bool UseRegistryPosition { get; set; }
		public double Left { get; set; }
		public double Top { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public bool HasGameOverride(string key) => false;
		public DmdStyle Style { get; set; }
	}

	public class TestVirtualAlphaNumericDisplayConfig : IVirtualAlphaNumericDisplayConfig
	{
		public bool Enabled { get; set; }
		public bool StayOnTop { get; set; }
		public RasterizeStyleDefinition Style { get; set; }
		public void SetPosition(int displayNumber, VirtualDisplayPosition position)
		{
		}
	}

	public class TestPinDmd1Config : IPinDmd1Config
	{
		public bool Enabled { get; set; }
	}

	public class TestPinDmd2Config : IPinDmd2Config {
		public bool Enabled { get; set; }
	}

	public class TestPinDmd3Config : IPinDmd3Config
	{
		public bool Enabled { get; set; }
		public string Port { get; set; }
	}
	
	public class TestZeDMDConfig : IZeDMDConfig
	{
		public bool Enabled { get; set; }
		public bool Debug { get; set; }
		public int Brightness { get; set; }
		public int RgbOrder { get; set; }
		public string WifiAddress { get; set; }
		public int WifiPort { get; set; }
		public string WifiSsid { get; set; }
		public string WifiPassword { get; set; }
	}

	public class TestPin2DmdConfig : IPin2DmdConfig
	{
		public bool Enabled { get; set; }
		public int Delay { get; set; }
	}

	public class TestPixelcadeConfig : IPixelcadeConfig
	{
		public bool Enabled { get; set; }
		public string Port { get; set; }
		public ColorMatrix ColorMatrix { get; set; }
	}

	public class TestVideoConfig : IVideoConfig
	{
		public bool Enabled { get; set; }
		public string Path { get; set; }
	}

	public class TestGifConfig : IGifConfig
	{
		public bool Enabled { get; set; }
		public string Path { get; set; }
	}

	public class TestBitmapConfig : IBitmapConfig
	{
		public bool Enabled { get; set; }
		public string Path { get; set; }
	}

	public class TestVpdbConfig : IVpdbConfig
	{
		public bool Enabled { get; set; }
		public string EndPoint { get; set; }
	}

	public class TestBrowserConfig : IBrowserConfig
	{
		public bool Enabled { get; set; }
		public int Port { get; set; }
	}

	public class TestNetworkConfig : INetworkConfig
	{
		public bool Enabled { get; set; }
		public string Url { get; set; }
		public bool Retry { get; set; }
		public int RetryInterval { get; set; }
	}

	public class TestPinUpConfig : IPinUpConfig
	{
		public bool Enabled { get; set; }
		public string GameName { get; set; }
	}

	public class RawOutputConfig : IRawOutputConfig
	{
		public bool Enabled { get; set; }
	}
}
