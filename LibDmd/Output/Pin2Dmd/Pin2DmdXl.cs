namespace LibDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public class Pin2DmdXl : Pin2DmdBase, IRgb24Destination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "PIN2DMD XL";

		protected override string ProductString => "PIN2DMD XL";

		public override int DmdWidth { get; } = 192;
		public override int DmdHeight { get; } = 64;

		private static Pin2DmdXl _instance;
		
		private Pin2DmdXl()
		{
		}

		/// <summary>
		/// Returns the current instance of the PIN2DMD API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static Pin2DmdXl GetInstance(int outputDelay)
		{
			if (_instance == null) {
				_instance = new Pin2DmdXl { Delay = outputDelay };
			}
			_instance.Init();
			return _instance;
		}

		public void RenderRgb24(byte[] frame)
		{
			// split into sub frames
			var changed = CreateRgb24(DmdWidth, DmdHeight, frame, _frameBufferRgb24, 4, pin2dmdConfig.rgbseq);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferRgb24);
			}
		}

		public void ClearDisplay()
		{
			var buffer = new byte[6148];
			buffer[0] = 0x81;
			buffer[1] = 0xC3;
			buffer[2] = 0xE8;
			buffer[3] = 12;
			RenderRaw(buffer);
			System.Threading.Thread.Sleep(Delay);
		}
	}
}
