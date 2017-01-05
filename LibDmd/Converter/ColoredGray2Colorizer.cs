using System;
using System.Collections;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät zwei Bit Graischtuifä-Frames i ColoredGray2-Frames umwandlä.
	/// </summary>
	public class ColoredGray2Colorizer : Gray2Colorizer, IConverter<Tuple<byte[][], Color[]>>
	{
		public override string Name { get; } = "Colored Gray4 Colorizer";
		protected override int BitLength { get; } = 4;
		public new RenderBitLength From { get; } = RenderBitLength.Gray4;
		public new RenderBitLength To { get; } = RenderBitLength.ColoredGray4;

		public ColoredGray2Colorizer(int width, int height, Coloring coloring, Animation[] animations = null)
			: base(width, height, coloring, animations)
		{
		}

		public new Tuple<byte[][], Color[]> Convert(byte[] frame)
		{
			var planes = HashFrame(frame);
			return planes == null ? null : new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(BitLength));
		}

		protected override void StartAnimation() {
			CurrentAnimation.Start(ColoredGray4AnimationFrames, Palette, () => LastChecksum = 0x0);
		}
	}
}
