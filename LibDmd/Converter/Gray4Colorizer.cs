using System.Collections;
using LibDmd.Common;
using LibDmd.Converter.Colorize;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i RGB24-Frames umwandlä.
	/// </summary>
	/// 
	/// <remarks>
	/// Fir Viärbit-Biuder git's kä Ergänzig unds einzigä wo cha 
	/// passiärä isch das ä kompletti Animazion abgschpiut wird.
	/// </remarks>
	public class Gray4Colorizer : AbstractColorizer, IConverter
	{
		protected override int BitLength { get; } = 4;
		public RenderBitLength From { get; } = RenderBitLength.Gray4;
		public RenderBitLength To { get; } = RenderBitLength.Rgb24;

		public Gray4Colorizer(int width, int height, Coloring coloring, Animation[] animations = null) : base(width, height, coloring, animations)
		{
		}

		public byte[] Convert(byte[] frame)
		{
			// Zersch dimmer s Frame i Planes uifteilä
			var planes = FrameUtil.Split(Width, Height, 4, frame);
			var match = false;

			// Jedi Plane wird einisch duräghäscht
			for (var i = 0; i < 4; i++) {
				var checksum = FrameUtil.Checksum(planes[i]);

				// Wemer dr Häsch hett de luägemr grad obs ächt äs Mäpping drzuäg git
				match = ApplyMapping(checksum, "unmasked");

				// Faus ja de grad awändä und guät isch
				if (match) {
					break;
				}
			}

			// Faus nei de gemmr Maskä fir Maskä durä und luägid ob da eppis passt
			if (!match && Coloring.Masks.Length > 0) {
				var maskedPlane = new byte[512];
				for (var i = 0; i < 4; i++) {
					foreach (var mask in Coloring.Masks) {
						var plane = new BitArray(planes[i]);
						plane.And(new BitArray(mask)).CopyTo(maskedPlane, 0);
						var checksum = FrameUtil.Checksum(maskedPlane);
						if (ApplyMapping(checksum, "masked")) {
							break;
						}
					}
				}
			}

			// Wenn än Animazion am laifä nisch de wird niid zrugg gäh
			if (IsAnimationRunning) {
				return null;
			}

			// Faus nid timmr eifach iifärbä.
			ColorUtil.ColorizeFrame(Width, Height, frame, Palette.Value, ColoredFrame);
			return ColoredFrame;
		}
	}
}
