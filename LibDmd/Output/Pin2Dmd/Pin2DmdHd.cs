namespace LibDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public class Pin2DmdHd : Pin2DmdBase, IRgb24Destination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "PIN2DMD HD";
		protected override string ProductString => "PIN2DMD HD";
		public override int DmdWidth { get; } = 256;
		public override int DmdHeight { get; } = 64;

		private static Pin2DmdHd _instance;

		private Pin2DmdHd()
		{
		}

		/// <summary>
		/// Returns the current instance of the PIN2DMD API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static Pin2DmdHd GetInstance(int outputDelay)
		{
			if (_instance == null) {
				_instance = new Pin2DmdHd { Delay = outputDelay };
			}
			_instance.Init();
			return _instance;
		}

		private static bool CreateRgb24HD(int width, int height, byte[] frame, byte[] frameBuffer, int offset, int rgbSequence, int buffermode)
		{
			var tmp = new byte[frameBuffer.Length];
			var identical = true;
			CreateRgb24(width, height, frame, tmp, offset, rgbSequence);
			var dest_idx = offset;
			var tmp_idx = offset;

			if (buffermode == 0)
			{
				for (int l = 0; l < (frameBuffer.Length - 4) / 2; l++)
				{
					identical = identical && frameBuffer[dest_idx] == tmp[tmp_idx] && frameBuffer[dest_idx + 1] == tmp[tmp_idx + (width / 2)];
					frameBuffer[dest_idx] = tmp[tmp_idx + (width / 2)];
					frameBuffer[dest_idx + 1] = (byte)(tmp[tmp_idx] << 1);
					dest_idx += 2;
					tmp_idx++;
					if ((dest_idx - offset) % width == 0)
						tmp_idx += width / 2;
				}
			}
			else
			{
				byte val;
				for (int i = 0; i < 32; i++)
				{  // 32 rows of source as we split into upper and lower half
					for (int j = 0; j < 16; j++)
					{ // 16 channels per driver IC with duplicate pixel
						for (int k = 0; k < 8; k++)
						{ // 8 led driver ICs per module
							for (int l = 5; l >= 0; l--)
							{
								tmp_idx = k * 16 + j + i * 256 + offset;
								val = tmp[tmp_idx + (width / 2) + (width * height / 2 * l)];
								identical = identical && frameBuffer[dest_idx] == val;
								frameBuffer[dest_idx++] = val;
								val = (byte)(tmp[tmp_idx + (width * height / 2 * l)] << 1);
								identical = identical && frameBuffer[dest_idx] == val;
								frameBuffer[dest_idx++] = val;
							}
						}
					}
				}
			}
			return !identical;
		}

		public void RenderRgb24(byte[] frame)
		{
			// split into sub frames
			var changed = CreateRgb24HD(DmdWidth, DmdHeight, frame, _frameBufferRgb24, 4, pin2dmdConfig.rgbseq, pin2dmdConfig.buffermode);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferRgb24);
			}
		}

		public void ClearDisplay()
		{
			var buffer = new byte[2052];
			buffer[0] = 0x81;
			buffer[1] = 0xC3;
			buffer[2] = 0xE7;
			buffer[3] = 0x00;
			RenderRaw(buffer);
			System.Threading.Thread.Sleep(Delay);
		}
	}
}
