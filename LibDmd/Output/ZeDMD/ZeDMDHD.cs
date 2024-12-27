using LibDmd.Frame;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMDHD : ZeDMDBase, IRgb24Destination, IRgb565Destination, IFixedSizeDestination
	{
		public override string Name => "ZeDMD HD";
		public virtual Dimensions FixedSize { get; } = new Dimensions(256, 64);
		public virtual bool DmdAllowHdScaling { get; protected set; } = true;

		private static ZeDMDHD _instance;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMDHD GetInstance(bool debug, int brightness, int rgbOrder, string port, bool scaleRgb24)
		{
			if (_instance == null) {
				_instance = new ZeDMDHD { Debug = debug, Brightness = brightness, RgbOrder = rgbOrder, Port = port, ScaleRgb24 = scaleRgb24 };
			}

			_instance.Init();
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		protected ZeDMDHD()
		{
		}

		protected new void Init()
		{
			base.Init();
			OpenUSBConnection();
			SendConfiguration();
			ZeDMD_SetFrameSize(_pZeDMD, FixedSize.Width, FixedSize.Height);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			DmdAllowHdScaling = ScaleRgb24;
			if (!ScaleRgb24) { ZeDMD_DisableUpscaling(_pZeDMD); }
			ZeDMD_RenderRgb888(_pZeDMD, frame.Data);
		}

		public void RenderRgb565(DmdFrame frame)
		{
			DmdAllowHdScaling = true;
			ZeDMD_EnableUpscaling(_pZeDMD);
			ZeDMD_RenderRgb565(_pZeDMD, frame.Data);
		}
	}
}
