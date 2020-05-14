namespace LibDmd.Output.Pin2Dmd
{
	public class Pin2DmdXL/* : Pin2DmdBase, IFixedSizeDestination*/
	{
/*

		protected override void SetupFrameBuffers()
		{
			// 4 bits per pixel plus 4 init bytes
			var size = (FixedSize.Surface * 4 / 8) + 4;
			_frameBufferGray4 = new byte[size];
			_frameBufferGray4[0] = 0x81; // frame sync bytes
			_frameBufferGray4[1] = 0xC3;
			_frameBufferGray4[2] = (byte)(_isXL ? 0xE8 : 0xE7);
			_frameBufferGray4[3] = 0x00;
		}


		public void ClearDisplay()
		{
			var buffer = new byte[_isXL ? 6148 : 2052];
			buffer[0] = 0x81;
			buffer[1] = 0xC3;
			buffer[2] = (byte)(_isXL ? 0xE8 : 0xE7);
			buffer[3] = (byte)(_isXL ? 0x0C : 0x00);
			RenderRaw(buffer);
			Thread.Sleep(Delay);
		}*/
	}
}
