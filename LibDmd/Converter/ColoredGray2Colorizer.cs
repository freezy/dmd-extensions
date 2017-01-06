using System;
using System.Collections;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using LibDmd.Input;
using LibDmd.Output;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät zwei Bit Graischtuifä-Frames i ColoredGray2-Frames umwandlä.
	/// </summary>
	public class ColoredGray2Colorizer : Gray2Colorizer, IConverter, IColoredGray2Source, IColoredGray4Source
	{
		public override string Name { get; } = "Colored Gray4 Colorizer";
		protected override int BitLength { get; } = 2;
		public new RenderBitLength From { get; } = RenderBitLength.Gray2;

		public ColoredGray2Colorizer(int width, int height, Coloring coloring, Animation[] animations = null)
			: base(width, height, coloring, animations)
		{
		}

		public new void Convert(byte[] frame)
		{
			var planes = HashFrame(frame);

			if (planes == null) {
				return;
			}

			// Wenns kä Erwiiterig gä hett, de gäbemer eifach d Planes mit dr Palettä zrugg
			if (planes.Length == BitLength) {
				ColoredGray2AnimationFrames.OnNext(new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(BitLength)));
			}

			// Faus scho, de schickermr s Frame uifd entsprächendi Uisgab faus diä gsetzt isch
			if (planes.Length == 4) {
				ColoredGray4AnimationFrames.OnNext(new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(planes.Length)));
			}
		}

		protected override void StartAnimation() {
			CurrentAnimation.Start(ColoredGray2AnimationFrames, ColoredGray4AnimationFrames, Palette, () => LastChecksum = 0x0);
		}
	}
}
