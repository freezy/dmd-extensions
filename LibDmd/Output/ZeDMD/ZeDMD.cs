﻿using LibDmd.Frame;

namespace LibDmd.Output.ZeDMD
{
	/// <summary>
	/// ZeDMD - real DMD with LED matrix display controlled with a cheap ESP32.
	/// Check "ZeDMD Project Page" https://github.com/PPUC/ZeDMD) for details.
	/// This implementation supports ZeDMD and ZeDMD HD.
	/// </summary>
	public class ZeDMD : ZeDMDUsbBase, IRgb24Destination, IRgb565Destination, IFixedSizeDestination
	{
		public override string Name => "ZeDMD";
		public Dimensions FixedSize { get; } = new Dimensions(128, 32);
		public virtual bool DmdAllowHdScaling { get; } = false;

		private static ZeDMD _instance;

		/// <summary>
		/// Returns the current instance of ZeDMD.
		/// </summary>
		/// <returns>New or current instance</returns>
		public static ZeDMD GetInstance(bool debug, int brightness, string port)
		{
			if (_instance == null)
			{
				_instance = new ZeDMD { Debug = debug, Brightness = brightness, Port = port };
			}

			_instance.Init();
			return _instance;
		}

		private new void Init()
		{
			base.Init();
			if (IsAvailable) {
				SendConfiguration();
				ZeDMD_SetFrameSize(_pZeDMD, FixedSize.Width, FixedSize.Height);
				ClearDisplay();
			}
		}
	}
}
