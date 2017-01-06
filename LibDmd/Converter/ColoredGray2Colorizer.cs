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
	public class ColoredGray2Colorizer : Gray2Colorizer, IConverter<Tuple<byte[][], Color[]>>
	{
		public override string Name { get; } = "Colored Gray4 Colorizer";
		protected override int BitLength { get; } = 2;
		public new RenderBitLength From { get; } = RenderBitLength.Gray2;
		public new RenderBitLength To { get; } = RenderBitLength.ColoredGray2;

		/// <summary>
		/// Faus äs en Erwiiterig git, odr än Animazion viärbittig isch
		/// </summary>
		public Subject<Tuple<byte[][], Color[]>> Gray4Source { get; set; }

		/// <summary>
		/// Und dä bruichr mr im obärä Fau aber wenn dr Output käi ColoredGray4 cha
		/// </summary>
		public Subject<byte[]> Rgb24FallbackSource { get; set; }

		public ColoredGray2Colorizer(int width, int height, Coloring coloring, Animation[] animations = null)
			: base(width, height, coloring, animations)
		{
		}

		public new Tuple<byte[][], Color[]> Convert(byte[] frame)
		{
			var planes = HashFrame(frame);

			if (planes == null) {
				return null;
			}

			// Wenns kä Erwiiterig gä hett, de gäbemer eifach d Planes mit dr Palettä zrugg
			if (planes.Length == BitLength) {
				return new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(BitLength));
			}

			// Faus scho, de schickermr s Frame uifd entsprächendi Uisgab faus diä gsetzt isch
			if (planes.Length == 4 && Gray4Source != null) {
				Gray4Source.OnNext(new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(planes.Length)));
				return null;
			}

			// Faus äs diä nit git de versuächämr s RGB24 Backup
			if (Rgb24FallbackSource != null) {
				Rgb24FallbackSource.OnNext(ColorUtil.ColorizeFrame(Width, Height, FrameUtil.Join(Width, Height, planes), Palette.Value.GetColors(planes.Length)));
				return null;
			}
			
			// Und sisch dimmr haut numä 2 Bits iifärbä
			byte[][] p = {planes[0], planes[1]};
			return new Tuple<byte[][], Color[]>(p, Palette.Value.GetColors(BitLength));
		}

		protected override void StartAnimation() {
			CurrentAnimation.Start(ColoredGray2AnimationFrames, Gray4Source, Rgb24FallbackSource, Palette, () => LastChecksum = 0x0);
		}
	}
}
