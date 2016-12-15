using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät Graischtuifä-Frames i RGB24-Frames umwandlä.
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
	/// abzschpilä. Dr obä gnannti Fau wär midem Modus 0. Fir Zwäibit-Datä gits
	/// nu ä Modus eis und zwei, und fir Viärbit-Datä numä dr Modus eis.
	/// 
	/// Wärendem än Animazion ablaift gaht i jedem Fau s Häsching uifd (eventuel 
	/// unsichtbarä) Datä vo VPM wiitr, das heisst dass Palettäwächsu odr sogar
	/// nii Animazionä chend losgah.
	/// </remarks>
	public abstract class AbstractColorizer 
	{
		public readonly int Width;
		public readonly int Height;

		protected readonly Coloring Coloring;
		protected readonly Animation[] Animations;
		protected readonly byte[] ColoredFrame;
		protected readonly BehaviorSubject<Color[]> Palette = new BehaviorSubject<Color[]>(new[]{Colors.Black, Colors.Coral});

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		protected Animation CurrentAnimation;
		protected Animation CurrentEnhancer;
		protected readonly Subject<byte[]> AnimationFrames = new Subject<byte[]>();
		protected bool IsAnimationRunning => CurrentAnimation != null && CurrentAnimation.IsRunning;
		protected bool IsEnhancerRunning => CurrentEnhancer != null && CurrentEnhancer.IsRunning;

		protected AbstractColorizer(int width, int height, Coloring coloring, Animation[] animations)
		{
			Width = width;
			Height = height;
			Coloring = coloring;
			Animations = animations;
			ColoredFrame = new byte[width * height * 3];
			SetPalette(Coloring.DefaultPalette.Colors);
			Logger.Debug("[colorize] Initialized.");
		}

		/// <summary>
		/// Luägt obdr Häsch neimä umä isch und tuät je nach Modus Ziigs machä.
		/// </summary>
		/// <param name="checksum">Dr Häsch</param>
		/// <param name="masked">Zum scheen loggä</param>
		/// <returns>Wenn eppis gladä wordä isch de <c>true</c>, sisch <c>false</c>.</returns>
		protected bool ApplyMapping(uint checksum, string masked)
		{
			var mapping = Coloring.FindMapping(checksum);

			// Wenn niid gfundä de tschüss
			if (mapping == null) {
				return false;
			}

			// Iifärbä dimmer i jedem Fau
			var palette = Coloring.GetPalette(mapping.PaletteIndex);
			if (palette == null) {
				Logger.Warn("[colorize] No palette found at index {0} for {1} frame.", mapping.PaletteIndex, masked);
				return false;
			}
			Logger.Info("[colorize] Setting palette of {0} colors via {1} frame.", palette.Colors.Length, masked);
			SetPalette(palette.Colors);

			// Numä iifärbä (hemmr scho) und guät isch
			if (mapping.Mode == 0) {
				return true;
			}

			// Än Animazion wird losgla
			if (mapping.Mode == 1) {
				if (mapping.Duration >= Animations.Length) {
					Logger.Warn("[colorize] No animation found at index {0} for {1} frame.", mapping.Duration, masked);
					return false;
				}
				Logger.Info("[colorize] Playing animation of {0} frames via {1} frame.", Animations[mapping.PaletteIndex].Frames.Length, masked);
				CurrentAnimation?.Stop();
				CurrentEnhancer?.Stop();
				CurrentAnimation = Animations[mapping.Duration];
				CurrentAnimation.Start(AnimationFrames, Palette);
				return true;
			}

			// Ab etz wärdid d Biudli mit zwe Bit ergänzt
			if (mapping.Mode == 2) {
				if (mapping.Duration >= Animations.Length) {
					Logger.Warn("[colorize] No animation found at index {0} for {1} frame.", mapping.Duration, masked);
					return false;
				}
				Logger.Info("[colorize] Enhancing animation of {0} frames via {1} frame.", Animations[mapping.PaletteIndex].Frames.Length, masked);
				CurrentAnimation?.Stop();
				CurrentEnhancer?.Stop();
				CurrentEnhancer = Animations[mapping.Duration];
				CurrentEnhancer.Start();
			}

			return false;
		}

		/// <summary>
		/// Tuät nii Farbä dr Palettä wo grad bruichd wird zuäwiisä.
		/// </summary>
		/// <param name="colors">Diä niiä Farbä vord Palettä</param>
		public void SetPalette(Color[] colors)
		{
			if (colors == null) {
				Logger.Warn("[colorize] Ignoring null palette.");
			}
			Logger.Debug("[colorize] Setting new palette:");
			Array.ForEach(colors, c => Logger.Trace("   " + c));

			Palette.OnNext(ColorUtil.GetPalette(colors, 16));
		}

		/// <summary>
		/// Tuät d Palettä wo grad bruichd wird mitärän andärä uiswächslä.
		/// </summary>
		/// <param name="index">Dr Index fo dr niiä Palettä wo vom Palettä-Feil gläsä wordä isch</param>
		public void LoadPalette(uint index)
		{
			var palette = Coloring.GetPalette(index);
			if (palette != null) {
				Logger.Info("[colorize] Setting palette of {0} colors via side channel...", palette.Colors.Length);
				SetPalette(palette.Colors);

			} else {
				Logger.Warn("[colorize] No palette with index {0} found to load through side channel.", index);
			}
		}

		public IObservable<byte[]> GetRgb24Frames()
		{
			return AnimationFrames;
		}

	}
}
