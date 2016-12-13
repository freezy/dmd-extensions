using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using NLog;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i RGB24-Frames umwandlä.
	/// </summary>
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
	/// </remarks>
	public class Gray4Colorizer : IConverter
	{
		public readonly int Width;
		public readonly int Height;
		public readonly Color DefaultColor = Colors.DarkOrchid;

		public RenderBitLength From { get; } = RenderBitLength.Gray4;
		public RenderBitLength To { get; } = RenderBitLength.Rgb24;

		private readonly Coloring _coloring;
		private readonly byte[] _coloredFrame;
		private Color[] _palette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Gray4Colorizer(int width, int height, string filename)
		{
			Logger.Info("[colorize] Loading palette file at {0}...", filename);
			Width = width;
			Height = height;
			_coloring = new Coloring(filename);
			_coloredFrame = new byte[width * height * 3];
			SetPalette(new[] { Colors.Black, DefaultColor });
			Logger.Debug("[colorize] Initialized.");
		}

		public byte[] Convert(byte[] frame)
		{
			// Zersch dimmer s Frame i Planes uifteilä
			var planes = FrameUtil.Split4Bit(Width, Height, frame);
			var match = false;

			// Jedi Plane wird einisch duräghäscht
			for (var i = 0; i < 3; i++) {
				var checksum = FrameUtil.Checksum(planes[i]);

				// Wemer dr Häsch hett de luägemr grad obs ächt ä Palettä drzuäg git
				var palette = _coloring.FindPalette(checksum);
				if (palette == null) {
					continue;
				}

				// Faus ja de grad awändä und guät isch
				Logger.Info("[colorize] Setting palette of {0} colors via unmasked frame.", palette.Colors.Length);
				SetPalette(palette.Colors);
				match = true;
				break;
			}
			// Faus nei de gemmr Maskä fir Maskä durä und luägid ob da eppis passt
			if (!match && _coloring.Masks.Length > 0) {
				var maskedPlane = new byte[512];
				for (var i = 0; i < 3; i++) {
					foreach (var mask in _coloring.Masks) {
						var plane = new BitArray(planes[i]);
						plane.And(new BitArray(mask)).CopyTo(maskedPlane, 0);
						var checksum = FrameUtil.Checksum(maskedPlane);
						var palette = _coloring.FindPalette(checksum);
						if (palette == null) {
							continue;
						}
						Logger.Info("[colorize] Setting palette of {0} colors via masked frame.", palette.Colors.Length);
						SetPalette(palette.Colors);
						match = true;
						break;
					}
					if (match) {
						break;
					}
				}
			}

			// Und am Schluss wird iigfärbt.
			ColorUtil.ColorizeFrame(Width, Height, frame, _palette, _coloredFrame);
			return _coloredFrame;
		}

		/// <summary>
		/// Tuät nii Farbä dr Palettä wo grad bruichd wird zuäwiisä.
		/// </summary>
		/// <param name="colors">Diä niiä Farbä vord Palettä</param>
		public void SetPalette(Color[] colors)
		{
			Logger.Debug("[colorize] Setting new palette:");
			Array.ForEach(colors, c => Logger.Trace("   " + c));

			_palette = ColorUtil.GetPalette(colors, 16);
		}

		/// <summary>
		/// Tuät d Palettä wo grad bruichd wird mitärän andärä uiswächslä.
		/// </summary>
		/// <param name="index">Dr Index fo dr niiä Palettä wo vom Palettä-Feil gläsä wordä isch</param>
		public void LoadPalette(uint index)
		{
			var palette = _coloring.GetPalette(index);
			if (palette != null) {
				Logger.Info("[colorize] Setting palette of {0} colors via side channel...", palette.Colors.Length);
				SetPalette(palette.Colors);

			} else {
				Logger.Warn("[colorize] No palette with index {0} found to load through side channel.", index);
			}
		}
	}
}
