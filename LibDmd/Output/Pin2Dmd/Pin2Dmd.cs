using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;

namespace LibDmd.Output.Pin2Dmd
{
	/// <summary>
	/// Output target for PIN2DMD devices.
	/// </summary>
	/// <see cref="https://github.com/lucky01/PIN2DMD"/>
	public class Pin2Dmd : Pin2DmdBase, IGray2Destination, IGray4Destination, IColoredGray2Destination, IColoredGray4Destination, IColoredGray6Destination, IRgb24Destination, IRawOutput, IFixedSizeDestination
	{
		public string Name { get; } = "PIN2DMD";
		protected override string ProductString => "PIN2DMD";
		public override Dimensions FixedSize { get; } = new Dimensions(128, 32);
		public bool DmdAllowHdScaling { get; set; } = true;

		private byte[] _frameBufferGray4;
		private byte[] _frameBufferGray6;
		private readonly byte[] _colorPalette;
		private readonly byte[] _colorPalette16;
		private readonly byte[] _colorPalette64;
		private static Pin2Dmd _instance;

		private Pin2Dmd()
		{
			// color palette
			_colorPalette = new byte[2052];
			_colorPalette[0] = 0x81;
			_colorPalette[1] = 0xC3;
			_colorPalette[2] = 0xE7;
			_colorPalette[3] = 0xFF;
			_colorPalette[4] = 0x04;
			_colorPalette[5] = 0x00;
			_colorPalette[6] = 0x01;

			// New firmware color palette
			_colorPalette16 = new byte[64];
			_colorPalette16[0] = 0x01;
			_colorPalette16[1] = 0xc3;
			_colorPalette16[2] = 0xe7;
			_colorPalette16[3] = 0xfe;
			_colorPalette16[4] = 0xed;
			_colorPalette16[5] = 0x10;

			// New firmware color palette
			_colorPalette64 = new byte[256];
			_colorPalette64[0] = 0x01;
			_colorPalette64[1] = 0xc3;
			_colorPalette64[2] = 0xe7;
			_colorPalette64[3] = 0xfe;
			_colorPalette64[4] = 0xed;
			_colorPalette64[5] = 0x40;
		}
		

		/// <summary>
		/// Returns the current instance of the PIN2DMD API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static Pin2Dmd GetInstance(int outputDelay)
		{
			if (_instance == null) {
				_instance = new Pin2Dmd { Delay = outputDelay };
			}
			_instance.Init();
			return _instance;
		}

		protected override void InitFrameBuffers()
		{
			base.InitFrameBuffers();

			// 4 bits per pixel plus 4 init bytes
			var size = (FixedSize.Surface * 4 / 8) + 4;
			_frameBufferGray4 = new byte[size];
			_frameBufferGray4[0] = 0x81; // frame sync bytes
			_frameBufferGray4[1] = 0xC3;
			_frameBufferGray4[2] = 0xE7;
			_frameBufferGray4[3] = 0x00;

			// 6 bits per pixel plus 4 init bytes
			size = (FixedSize.Surface * 6 / 8) + 4;
			_frameBufferGray6 = new byte[size];
			_frameBufferGray6[0] = 0x81; // frame sync bytes
			_frameBufferGray6[1] = 0xC3;
			_frameBufferGray6[2] = 0xE8;
			_frameBufferGray6[3] = 0x06;
		}

		public void RenderGray2(byte[] frame)
		{
			// 2-bit frames are rendered as 4-bit
			RenderGray4(FrameUtil.ConvertGrayToGray(frame, new byte[] { 0x0, 0x1, 0x4, 0xf }));
		}

		public void RenderGray4(byte[] frame)
		{
			// convert to bit planes
			var planes = FrameUtil.Split(FixedSize, 4, frame);

			// copy to buffer
			var changed = FrameUtil.Copy(planes, _frameBufferGray4, 4);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferGray4);
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			// split into sub frames
			var changed = CreateRgb24(FixedSize, frame, _frameBufferRgb24, 4, pin2dmdConfig.rgbseq);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferRgb24);
			}
		}

		public void RenderColoredGray6(ColoredFrame frame)
		{
			SetPalette(frame.Palette, frame.PaletteIndex);

			// copy to buffer
			var changed = FrameUtil.Copy(frame.Planes, _frameBufferGray6, 4);

			// send frame buffer to device
			if (changed)
			{
				RenderRaw(_frameBufferGray6);
			}
		}

		public void RenderColoredGray4(ColoredFrame frame)
		{
			SetPalette(frame.Palette, frame.PaletteIndex);

			// copy to buffer
			var changed = FrameUtil.Copy(frame.Planes, _frameBufferGray4, 4);

			// send frame buffer to device
			if (changed) {
				RenderRaw(_frameBufferGray4);
			}
		}

		public void RenderColoredGray2(ColoredFrame frame)
		{
			SetPalette(frame.Palette, frame.PaletteIndex);

			var joinedFrame = FrameUtil.Join(FixedSize, frame.Planes);

			// send frame buffer to device
			RenderGray4(FrameUtil.ConvertGrayToGray(joinedFrame, new byte[] { 0x0, 0x1, 0x4, 0xf }));
		}


		protected override void SetSinglePalette(Color[] colors)
		{
			var numOfColors = colors.Length;
			var palette = ColorUtil.GetPalette(colors, numOfColors);
			var pos = 6;

			if (numOfColors == 2)
			{
				pos = 7;
				var color0 = palette[0];
				var color15 = palette[1];
				_colorPalette[pos] = color0.R;
				_colorPalette[pos + 1] = color0.G;
				_colorPalette[pos + 2] = color0.B;

				for (int i = 1; i < 15; i++)
				{
					pos = 7 + (i * 3);
					_colorPalette[pos] = (byte)((color0.R / 15 * i) + ((color15.R / 15) * i));
					_colorPalette[pos + 1] = (byte)((color0.G / 15 * i) + ((color15.G / 15) * i));
					_colorPalette[pos + 2] = (byte)((color0.B / 15 * i) + ((color15.B / 15) * i));
				}

				pos = 7 + (15 * 3); // color 15
				color15 = palette[1];
				_colorPalette[pos] = color15.R;
				_colorPalette[pos + 1] = color15.G;
				_colorPalette[pos + 2] = color15.B;
				RenderRaw(_colorPalette);
				System.Threading.Thread.Sleep(Delay);
			}

			if (numOfColors == 4)
			{
				var color = palette[0];
				_colorPalette16[pos] = color.R;
				_colorPalette16[pos + 1] = color.G;
				_colorPalette16[pos + 2] = color.B;
				color = palette[1];
				pos = 6+3; // color 1
				_colorPalette16[pos] = color.R;
				_colorPalette16[pos + 1] = color.G;
				_colorPalette16[pos + 2] = color.B;
				pos = 6+12; // color 4
				color = palette[2];
				_colorPalette16[pos] = color.R;
				_colorPalette16[pos + 1] = color.G;
				_colorPalette16[pos + 2] = color.B;
				pos = 6+45; // color 15
				color = palette[3];
				_colorPalette16[pos] = color.R;
				_colorPalette16[pos + 1] = color.G;
				_colorPalette16[pos + 2] = color.B;
				RenderRaw(_colorPalette16);
			}

			if (numOfColors == 16)
			{
				for (var i = 0; i < 16; i++)
				{
					var color = palette[i];
					_colorPalette16[pos] = color.R;
					_colorPalette16[pos + 1] = color.G;
					_colorPalette16[pos + 2] = color.B;
					pos += 3;
				}
				RenderRaw(_colorPalette16);
			}
			if (numOfColors == 64)
			{
				for (var i = 0; i < 64; i++)
				{
					var color = palette[i];
					_colorPalette64[pos] = color.R;
					_colorPalette64[pos + 1] = color.G;
					_colorPalette64[pos + 2] = color.B;
					pos += 3;
				}
				RenderRaw(_colorPalette64);
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
