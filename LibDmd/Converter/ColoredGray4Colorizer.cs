using System;
using System.Collections;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using LibDmd.Input;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i ColoredGray4-Frames umwandlä.
	/// </summary>
	public class ColoredGray4Colorizer : Gray4Colorizer, IConverter, IColoredGray2Source, IColoredGray4Source
	{
		public override string Name { get; } = "Colored Gray4 Colorizer";
		protected override int BitLength { get; } = 4;
		public new RenderBitLength From { get; } = RenderBitLength.Gray4;
		public new RenderBitLength To { get; } = RenderBitLength.ColoredGray4;

		public ColoredGray4Colorizer(int width, int height, Coloring coloring, Animation[] animations = null)
			: base(width, height, coloring, animations)
		{
		}

		public new void Convert(byte[] frame)
		{
			var planes = HashFrame(frame);
			if (planes != null) {
				ColoredGray4AnimationFrames.OnNext(new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(BitLength)));
			}
		}

		protected override void StartAnimation() {
			CurrentAnimation.Start(ColoredGray2AnimationFrames, ColoredGray4AnimationFrames, Palette, () => LastChecksum = 0x0);
		}
	}
}
