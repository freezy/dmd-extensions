using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Media;
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
	public class Gray2Colorizer : AbstractColorizer, IConverter, IColoredGray2Source, IColoredGray4Source
	{
		public override string Name { get; } = "2 Bit Colorizer";
		protected override int BitLength { get; } = 2;

		public FrameFormat From { get; } = FrameFormat.Gray2;

		public Gray2Colorizer(int width, int height, Coloring coloring, Animation[] animations = null) : base(width, height, coloring, animations)
		{
		}

		public void Convert(byte[] frame)
		{
			var planes = HashFrame(frame);

			if (planes == null) {
				return;
			}

			// Wenns kä Erwiiterig gä hett, de gäbemer eifach d Planes mit dr Palettä zrugg
			if (planes.Length == 2) {
				ColoredGray2AnimationFrames.OnNext(new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(planes.Length)));
			}

			// Faus scho, de schickermr s Frame uifd entsprächendi Uisgab faus diä gsetzt isch
			if (planes.Length == 4) {
				ColoredGray4AnimationFrames.OnNext(new Tuple<byte[][], Color[]>(planes, Palette.Value.GetColors(planes.Length)));
			}
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
			var planes = FrameUtil.Split(Dimensions.Value.Width, Dimensions.Value.Height, 2, frame);

			// Enhancer isch um eis verschobä, das heisst äs wird erscht hiä gluägt (bim Frame nachem Häschä)
			if (IsEnhancerRunning) {
				
				var data = CurrentEnhancer.Next();
				
				// Wenns letschtä Frame vodr Animazion gsi isch de chemmr d Checksum wird resettä
				if (!CurrentEnhancer.IsRunning) {
					Logger.Trace("[timing] Waiting {0}ms for last frame to finish...", CurrentEnhancer.LastFrame.Delay);

					// nu uifs letschti biud wartä bis mer fertig sind
					Observable
						.Never<Unit>()
						.StartWith(Unit.Default)
						.Delay(TimeSpan.FromMilliseconds(CurrentEnhancer.LastFrame.Delay))
						.Subscribe(_ => AnimationFinished());
				}

				// Wenns Biud mit zwe Bytes muäss ergänzt wärdä de timmrd planes erwiitärä
				if (data.BitLength == 2) {
					planes = new []{ planes[0], planes[1], data.Planes[0], data.Planes[1] };

				} else {
					Logger.Warn("Got a bit enhancer that gave us a {0}-bit frame. Duh, ignoring.", data.BitLength);	
				}
				return planes;
			}

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
							match = true;
							break;
						}
					}
				}
			}
			if (!match) {
				LastChecksum = 0x0;
			}

			// Wenn än Animazion am laifä nisch de wird niid zrugg gäh
			if (IsAnimationRunning) {
				Logger.Trace("[timing] VPM Frame #{0} dropped ({1} ms).", FrameCounter++, (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - LastFrame);
				return null;
			}

			return planes;
		}
	}
}
