using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media;
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
	public abstract class AbstractColorizer : AbstractSource
	{
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }

		protected abstract int BitLength { get; }
		protected readonly Coloring Coloring;
		protected readonly Animation[] Animations;
		protected byte[] ColoredFrame;
		protected readonly BehaviorSubject<Palette> Palette = new BehaviorSubject<Palette>(new Palette(new[]{Colors.Black, Colors.Cyan}));

		protected Animation CurrentAnimation;
		protected Animation CurrentEnhancer;
		protected readonly Subject<Tuple<byte[][], Color[]>> ColoredGray2AnimationFrames = new Subject<Tuple<byte[][], Color[]>>();
		protected readonly Subject<Tuple<byte[][], Color[]>> ColoredGray4AnimationFrames = new Subject<Tuple<byte[][], Color[]>>();
		protected readonly Subject<byte[]> Rgb24AnimationFrames = new Subject<byte[]>();
		protected bool IsAnimationRunning => CurrentAnimation != null && CurrentAnimation.IsRunning;
		protected bool IsEnhancerRunning => CurrentEnhancer != null && CurrentEnhancer.IsRunning;
		protected int NumColors => (int)Math.Pow(2, BitLength);

		private Palette _defaultPalette;
		private IDisposable _paletteReset;
		protected int FrameCounter;
		protected long LastFrame;
		protected uint LastChecksum;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected AbstractColorizer(Coloring coloring, Animation[] animations)
		{
			Coloring = coloring;
			Animations = animations;
			SetPalette(Coloring.DefaultPalette, true);
			Logger.Debug("[colorize] Initialized.");
		}

		public void Init()
		{
			Dimensions.Subscribe(dim => ColoredFrame = new byte[dim.Width * dim.Height * 3]);
		}

		/// <summary>
		/// Luägt obdr Häsch neimä umä isch und tuät je nach Modus Ziigs machä.
		/// </summary>
		/// <param name="planes">The current frame</param>
		/// <param name="checksum">Dr Häsch</param>
		/// <param name="masked">Zum scheen loggä</param>
		/// <returns>Wenn eppis gladä wordä isch de <c>true</c>, sisch <c>false</c>.</returns>
		protected bool ApplyMapping(byte[][] planes, uint checksum, string masked)
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

			if (checksum != LastChecksum) {
				Logger.Info("[colorize] Setting palette {0} of {1} colors via {2} frame:", mapping.PaletteIndex, palette.Colors.Length, masked);
				_paletteReset?.Dispose();
				_paletteReset = null;
				SetPalette(palette);
			}
			
			if (mapping.Mode == 0) {
				// Numä iifärbä (hemmr scho) und guät isch
				if (mapping.Duration > 0) {
					_paletteReset = Observable
						.Never<Unit>()
						.StartWith(Unit.Default)
						.Delay(TimeSpan.FromMilliseconds(mapping.Duration)).Subscribe(_ => {
							if (_defaultPalette != null) {
								Logger.Info("[colorize] Resetting to default palette after {0} ms.", mapping.Duration);
								SetPalette(_defaultPalette);
							}
							_paletteReset = null;
						});
				}
				return true;
			}

			if (Animations == null) {
				Logger.Warn("[colorize] Colorizer does not contain any animations, skipping lookup for position {0}.", mapping.Duration);
				return false;
			}

			// Sisch wird än Animazion wird losgla
			var animation = Animation.Find(Animations, mapping.Duration);
			if (animation == null) {
				Logger.Warn("[colorize] No animation found at position {0} for {1} frame.", mapping.Duration, masked);
				return false;
			}

			// Modus eis: Äs cha si das än Animazion mehrmaus dr gliichi Häsch hat am Aafang, i dem Fau nid looslah.
			if (mapping.Mode == 1 && (CurrentAnimation == null || checksum != LastChecksum)) {
				Logger.Info("[colorize] Playing animation of {0} frames via {1} frame.", animation.NumFrames, masked);
				CurrentAnimation?.Stop();
				CurrentEnhancer?.Stop();
				CurrentAnimation = animation;
				CurrentAnimation.StartReplace(ColoredGray2AnimationFrames, ColoredGray4AnimationFrames, Palette, AnimationFinished);
				FrameCounter = 0;
				LastFrame = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			}

			// Modus zwei: Gliichi Kondizionä wiä obä, aber uifäas anders Objäkt
			if (mapping.Mode == 2 && (CurrentEnhancer == null || checksum != LastChecksum)) {
				Logger.Info("[colorize] Enhancing animation of {0} frames via {1} frame.", animation.NumFrames, masked);
				CurrentAnimation?.Stop();
				CurrentEnhancer?.Stop();
				CurrentEnhancer = animation;
				CurrentEnhancer.StartEnhance(planes, ColoredGray4AnimationFrames, Palette, AnimationFinished);
				FrameCounter = 0;
				LastFrame = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			}

			LastChecksum = checksum;
			return true;
		}

		/// <summary>
		/// Tuät nii Farbä dr Palettä wo grad bruichd wird zuäwiisä.
		/// </summary>
		/// <param name="palette">Diä nii Palettä</param>
		/// <param name="isDefault"></param>
		public void SetPalette(Palette palette, bool isDefault = false)
		{
			if (palette == null) {
				Logger.Warn("[colorize] Ignoring null palette.");
				return;
			}
			if (isDefault) {
				_defaultPalette = palette;
			}
			Logger.Debug("[colorize] Setting new palette: [ {0} ]", string.Join(" ", palette.Colors.Select(c => c.ToString())));
			Palette.OnNext(palette);
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
				SetPalette(palette);

			} else {
				Logger.Warn("[colorize] No palette with index {0} found to load through side channel.", index);
			}
		}

		/// <summary>
		/// Wird uisgfiährt wenn än Animazion fertig isch, cha irgend ä Modus si.
		/// </summary>
		protected void AnimationFinished()
		{
			//Logger.Trace("[timing] Animation finished.");
			LastChecksum = 0x0;
			SetPalette(_defaultPalette);
			CurrentAnimation = null;
			CurrentEnhancer = null;
		}

		public IObservable<byte[]> GetRgb24Frames()
		{
			return Rgb24AnimationFrames;
		}

		public IObservable<Tuple<byte[][], Color[]>> GetColoredGray2Frames()
		{
			return ColoredGray2AnimationFrames;
		}

		public IObservable<Tuple<byte[][], Color[]>> GetColoredGray4Frames()
		{
			return ColoredGray4AnimationFrames;
		}
	}
}
