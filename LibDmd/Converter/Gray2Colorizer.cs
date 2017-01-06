using System;
using System.Collections;
using System.Collections.Generic;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using LibDmd.Input;

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
	public class Gray2Colorizer : AbstractColorizer, IConverter, IRgb24Source
	{
		public override string Name { get; } = "Gray2-Colorizer";
		public RenderBitLength NativeFormat { get; } = RenderBitLength.Gray2;
		protected override int BitLength { get; } = 2;

		public RenderBitLength From { get; } = RenderBitLength.Gray2;

		public Gray2Colorizer(int width, int height, Coloring coloring, Animation[] animations = null) : base(width, height, coloring, animations)
		{
		}

		public void Convert(byte[] frame)
		{
			var planes = HashFrame(frame);
			if (planes == null) {
				return;
			}

			// Wenns erwiitered wordänisch, de dimmers Frame ersetzä
			if (planes.Length > BitLength) {
				frame = FrameUtil.Join(Width, Height, planes);
			}

			// Faus eppis zrugg cho isch timmr eifach iifärbä.
			ColorUtil.ColorizeFrame(Width, Height, frame, Palette.Value.GetColors(planes.Length), ColoredFrame);
			Rgb24AnimationFrames.OnNext(ColoredFrame);
		}

		/// <summary>
		/// Tuät luägä obs Frame mit und ohni Maskä mätscht. Faus ja de wirds
		/// grad aagwandt, d.h. Palettäwächsu odr Animazion, und d Planes womer
		/// berächnet hend zrugggäh, und faus nei gits null zrugg.
		/// </summary>
		/// <remarks>
		/// Äs cha si dass meh Planes as vorhär zruggäh wärdit, i dem Fau ischs
		/// Frame erwiitered wordä.
		/// </remarks>
		/// <param name="frame">S Frame wo grad cho isch</param>
		/// <returns></returns>
		protected byte[][] HashFrame(byte[] frame)
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

			// Wenn kä Erwiiterer am laifä nisch de simmer fertig
			if (!IsEnhancerRunning) {
				return planes;
			}

			// Wenns Biud mit zwe Bytes muäss ergänzt wärdä de timmrd planes erwiitärä
			var data = CurrentEnhancer.Next();
			if (data.BitLength == 2) {
				return new List<byte[]>(planes) { data.Planes[0], data.Planes[1] }.ToArray();
			}
			Logger.Warn("Got a bit enhancer that gave us a {0}-bit frame. Duh, ignoring.", data.BitLength);

			// Wenns letschtä Frame vodr Animazion gsi isch de chemmr d Checksum wird resettä
			if (!CurrentEnhancer.IsRunning) {
				LastChecksum = 0x0;
			}
			return planes;
		}

		protected override void StartAnimation() {
			CurrentAnimation.Start(Rgb24AnimationFrames, Palette, () => LastChecksum = 0x0);
		}
	}
}
