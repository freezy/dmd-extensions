using System;
using System.Collections;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i ColoredGray4-Frames umwandlä.
	/// </summary>
	public class ColoredGray4Colorizer : Gray4Colorizer, IConverter<Tuple<byte[][], Color[]>>
	{
		public override string Name { get; } = "Colored Gray4 Colorizer";
		protected override int BitLength { get; } = 4;
		public new RenderBitLength From { get; } = RenderBitLength.Gray4;
		public new RenderBitLength To { get; } = RenderBitLength.ColoredGray4;

		/// <summary>
		/// Es cha ja si, dass än Animazion 2-bit Frames het, de bruichr mr dä
		/// </summary>
		public Subject<Tuple<byte[][], Color[]>> Gray2Source { get; set; }

		public ColoredGray4Colorizer(int width, int height, Coloring coloring, Animation[] animations = null)
			: base(width, height, coloring, animations)
		{
		}

		public new Tuple<byte[][], Color[]> Convert(byte[] frame)
		{
			var planes = HashFrame(frame);
			return planes == null ? null : new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(BitLength));
		}

		protected override void StartAnimation() {
			CurrentAnimation.Start(Gray2Source, ColoredGray4AnimationFrames, null, Palette, () => LastChecksum = 0x0);
		}
	}
}
