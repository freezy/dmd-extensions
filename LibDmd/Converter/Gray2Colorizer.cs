using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i RGB24-Frames umwandlä.
	/// </summary>
	/// 
	/// <remarks>
	/// Hiä gits zwe Methodä. I jedem Fau wärdid aui Farbdatä zersch vomänä Feil
	/// gladä.
	/// 
	/// Im erschtä Fau wärdid d Palettäwächsu ibärä Sitäkanau aagäh. Diä Wächsu
	/// chemid diräkt vom ROM, wo midem Pinball Browser abgändered wordä sind.
	/// 
	/// Im zweitä Fau timmer jedes Biud häschä und luägid ob dr Häsch neimä im
	/// Feil vorhandä isch. Faus ja, de wird diä entsprächendi Palettä gladä. S Feil
	/// cha abr ai nu Maskä beinhautä wo dynamischi Elemänt uisbländid, diä wärdid
	/// de ai nu aagwandt bim Häschä.
	/// 
	/// Bim Häschä isch nu wichtig z wissä dass mr uifd Bitplanes seperat häschid,
	/// und nid uifd Originaldatä vo VPM. Bi drii Maskä und viär Bit git das auso
	/// drii mau viär plus viär unghäschti, macht sächzä Häsches zum Vrgliichä.
	/// 
	/// Näbdr Palettäwächsu gits abr ai nu ä Meglichkäit, kompletti Animazionä
	/// abzschpilä. Je nach <see cref="Mapping.Mode"/> wird Animazion komplett
	/// abgschpiut oder numä mit Graidatä ergänzt.
	/// 
	/// Wärendem än Animazion ablaift gaht abrs Häsching uifd (eventuel 
	/// unsichtbarä) Datä vo VPM wiitr, das heisst dass Palettäwächsu odr sogar
	/// nii Animazionä chend losgah.
	/// </remarks>
	public class Gray2Colorizer : AbstractColorizer, IConverter, IFrameSourceRgb24
	{

		public RenderBitLength From { get; } = RenderBitLength.Gray2;
		public RenderBitLength To { get; } = RenderBitLength.Rgb24;

		public Gray2Colorizer(int width, int height, Coloring coloring, Animation[] animations = null) : base(width, height, coloring, animations)
		{
			Animations[0].Start(AnimationFrames, Palette);
		}

		public byte[] Convert(byte[] frame)
		{
			// Wenn schonä Animation am laifä isch de gäbämr nid uisä
			if (IsAnimationRunning) {
				return null;
			}

			// Zersch dimmer s Frame i Planes uifteilä
			var planes = FrameUtil.Split(Width, Height, 2, frame);
			var match = false;

			// Jedi Plane wird einisch duräghäscht
			for (var i = 0; i < 2; i++) {
				var checksum = FrameUtil.Checksum(planes[i]);

				// Wemer dr Häsch hett de luägemr grad obs ächt äs Mäpping drzuäg git
				match = ApplyMapping(checksum, false);

				// Faus ja de grad awändä und guät isch
				if (match) {
					break;
				}
			}
			// Villicht het än Animation aagfangä..
			if (IsAnimationRunning) {
				return null;
			}

			// Faus nei de gemmr Maskä fir Maskä durä und luägid ob da eppis passt
			if (!match && Coloring.Masks.Length > 0) {
				var maskedPlane = new byte[512];
				for (var i = 0; i < 2; i++) {
					foreach (var mask in Coloring.Masks) {
						var plane = new BitArray(planes[i]);
						plane.And(new BitArray(mask)).CopyTo(maskedPlane, 0);
						var checksum = FrameUtil.Checksum(maskedPlane);
						if (ApplyMapping(checksum, true)) {
							break;
						}
					}
				}
			}

			// Villicht het ja etz än Animation aagfangä..
			if (IsAnimationRunning) {
				return null;
			}

			// Faus nid timmr eifach iifärbä.
			ColorUtil.ColorizeFrame(Width, Height, frame, Palette.Value, ColoredFrame);
			return ColoredFrame;
		}
	}
}
