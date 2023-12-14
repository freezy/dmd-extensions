using System.Windows.Media;
using LibDmd.Input;
using LibDmd.Common;
using LibDmd.Output.Virtual.AlphaNumeric;
using LibDmd.Output.Virtual.Dmd;

namespace LibDmd.DmdDevice
{
	public interface IConfiguration
	{
		IGlobalConfig Global { get; }
		IVirtualDmdConfig VirtualDmd { get; }
		IVirtualAlphaNumericDisplayConfig VirtualAlphaNumericDisplay { get; }
		IPinDmd1Config PinDmd1 { get; }
		IPinDmd2Config PinDmd2 { get; }
		IPinDmd3Config PinDmd3 { get; }
		IZeDMDConfig ZeDMD { get; }
		IZeDMDConfig ZeDMDHD { get; }
		IZeDMDWiFiConfig ZeDMDWiFi { get; }
		IZeDMDWiFiConfig ZeDMDHDWiFi { get; }
		IPin2DmdConfig Pin2Dmd { get; }
		IPixelcadeConfig Pixelcade { get; }
		IVideoConfig Video { get; }
		IGifConfig Gif { get; }
		IBitmapConfig Bitmap { get; }
		IVpdbConfig VpdbStream { get; }
		IBrowserConfig BrowserStream { get; }
		INetworkConfig NetworkStream { get; }
		IPinUpConfig PinUp { get; }
		IRawOutputConfig RawOutput  { get; }

		bool HasGameName { get; }
		string DataPath { get; }
		void Reload();
		void SetGameName(string gameName);
	}

	public interface IGlobalConfig
	{
		ResizeMode Resize { get; }
		bool FlipHorizontally { get; }
		bool FlipVertically { get; }
		bool Colorize { get; }
		bool QuitWhenDone { get; set; }
		int QuitAfter { get; set; }
		bool NoClear { get; }
		Color DmdColor { get; }
		ScalerMode ScalerMode { get; }
		ScalerMode VniScalerMode { get; }
		string VniKey { get; }
		bool SkipAnalytics { get; }
		PluginConfig[] Plugins { get; }
	}

	public interface IPinDmd1Config
	{
		bool Enabled { get; }
	}

	public interface IPinDmd2Config
	{
		bool Enabled { get; }
	}

	public interface IPinDmd3Config
	{
		bool Enabled { get; }
		string Port { get; }
	}

	public interface IZeDMDConfig
	{
		bool Enabled { get; }
		bool Debug { get; }
		int Brightness { get; }
		int RgbOrder { get; }
	}

	public interface IZeDMDWiFiConfig : IZeDMDConfig
	{
		string WifiAddress { get; }
		int WifiPort { get; }
		string WifiSsid { get; }
		string WifiPassword { get; }
	}

	public interface IPin2DmdConfig
	{
		bool Enabled { get; }
		int Delay { get; }
	}

	public interface IPixelcadeConfig
	{
		bool Enabled { get; }
		string Port { get; }
		ColorMatrix ColorMatrix { get; }
	}

	public interface IVirtualDmdConfig
	{
		bool Enabled { get; }
		bool StayOnTop { get; }
		bool IgnoreAr { get; }
		bool UseRegistryPosition { get; }
		double Left { get; }
		double Top { get; }
		double Width { get; }
		double Height { get; }
		bool HasGameOverride(string key);
		DmdStyle Style { get; }
	}

	public interface IVirtualAlphaNumericDisplayConfig
	{
		bool Enabled { get; }
		bool StayOnTop { get; }
		RasterizeStyleDefinition Style { get; }
		void SetPosition(int displayNumber, VirtualDisplayPosition position);
	}

	public interface IVideoConfig
	{
		bool Enabled { get; }
		string Path { get; }
	}

	public interface IGifConfig
	{
		bool Enabled { get; }
		string Path { get; }
	}

	public interface IBitmapConfig
	{
		bool Enabled { get; }
		string Path { get; }
	}

	public interface IBrowserConfig
	{
		bool Enabled { get; }
		int Port { get; }
	}

	public interface INetworkConfig
	{
		bool Enabled { get; }
		string Url { get; }
		bool Retry { get; }
		int RetryInterval { get; }
	}

	public interface IVpdbConfig
	{
		bool Enabled { get; }
		string EndPoint { get; }
	}

	public interface IPinUpConfig
	{
		bool Enabled { get; }
		string GameName { get; }
	}

	public interface IRawOutputConfig
	{
		bool Enabled { get; }
	}
}
