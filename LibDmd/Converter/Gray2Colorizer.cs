using System;
using System.Collections;
using System.Collections.Generic;
using LibDmd.Common;
using LibDmd.Converter.Colorize;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i RGB24-Frames umwandlä.
	/// </summary>
	/// 
	/// <remarks>
	/// Je nach <see cref="Mapping.Mode"/> wird d Animazion komplett
	/// abgschpiut oder numä mit Graidatä ergänzt.
	/// </remarks>
	public class Gray2Colorizer : AbstractColorizer, IConverter<byte[]>
	{
		public override string Name { get; } = "Gray2-Colorizer";
		protected override int BitLength { get; } = 2;
		public RenderBitLength From { get; } = RenderBitLength.Gray2;
		public RenderBitLength To { get; } = RenderBitLength.Rgb24;

		public Gray2Colorizer(int width, int height, Coloring coloring, Animation[] animations = null) : base(width, height, coloring, animations)
		{
		}

		public byte[] Convert(byte[] frame)
		{
			// Zersch dimmer s Frame i Planes uifteilä
			var planes = FrameUtil.Split(Width, Height, 2, frame);
			var match = false;

			// Jedi Plane wird einisch duräghäscht
			for (var i = 0; i < 2; i++) {
				var checksum = FrameUtil.Checksum(planes[i]);

				//FrameUtil.DumpBinary(Width, Height, planes[i]);
				//Logger.Trace("Hash bit {0}: {1}", i, checksum.ToString("X"));

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
				for (var i = 0; i < 2; i++) {
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
				Logger.Trace("[timing] VPM Frame #{0} dropped ({1} ms).", FrameCounter++, (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - LastFrame);
				return null;
			}

			// Wenns Biud mit zwe Bytes muäss ergänzt wärdä de timmrs einfach ersetzä
			var bitlength = BitLength;
			if (IsEnhancerRunning) {
				var data = CurrentEnhancer.Next();
				if (data.BitLength == 2) {
					bitlength += 2;
					var enhancedPlanes = new List<byte[]>(planes) { data.Planes[0], data.Planes[1] };
					frame = FrameUtil.Join(Width, Height, enhancedPlanes.ToArray());
					
				} else {
					Logger.Warn("Got a bit enhancer that gave us a {0}-bit frame. Duh, ignoring.", data.BitLength);
				}
				if (!CurrentEnhancer.IsRunning) {
					LastChecksum = 0x0;
				}
			}

			//Logger.Trace("Palette: [ {0} ]", string.Join(", ", Palette.Value.Select(color => color.ToString())));

			// Und sisch timmr eifach iifärbä.
			ColorUtil.ColorizeFrame(Width, Height, frame, Palette.Value.GetColors(bitlength), ColoredFrame);
			return ColoredFrame;
		}
	}
}
