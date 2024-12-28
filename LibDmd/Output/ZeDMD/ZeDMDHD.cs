using LibDmd.Frame;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMDHD : ZeDMDUsbBase, IRgb24Destination, IRgb565Destination, IFixedSizeDestination
	{
		public override string Name => "ZeDMD HD";
		public virtual Dimensions FixedSize { get; } = new Dimensions(256, 64);
		public virtual bool DmdAllowHdScaling { get; } = true;

		private static ZeDMDHD _instance;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMDHD GetInstance(bool debug, int brightness, int rgbOrder, string port)
		{
			if (_instance == null) {
				_instance = new ZeDMDHD { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port };
			}

			_instance.Init();
			return _instance;
		}

		protected new void Init()
		{
			base.Init();
			if (IsAvailable) {
				SendConfiguration();
				ZeDMD_SetFrameSize(_pZeDMD, FixedSize.Width, FixedSize.Height);
			}
		}
	}
}
