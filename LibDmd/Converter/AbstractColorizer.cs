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
	public abstract class AbstractColorizer : IRgb24Source
	{
		public readonly int Width;
		public readonly int Height;

		public string Name { get; } = "Colorizer";
		public BehaviorSubject<DisplaySize> Dimensions { get; }
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }

		protected abstract int BitLength { get; }
		protected readonly Coloring Coloring;
		protected readonly Animation[] Animations;
		protected readonly byte[] ColoredFrame;
		protected readonly BehaviorSubject<Palette> Palette = new BehaviorSubject<Palette>(new Palette(new[]{Colors.Black, Colors.Cyan}));

		protected Animation CurrentAnimation;
		protected Animation CurrentEnhancer;
		protected readonly Subject<byte[]> AnimationFrames = new Subject<byte[]>();
		protected bool IsAnimationRunning => CurrentAnimation != null && CurrentAnimation.IsRunning;
		protected bool IsEnhancerRunning => CurrentEnhancer != null && CurrentEnhancer.IsRunning;
		protected int NumColors => (int)Math.Pow(2, BitLength);

		private Palette _defaultPalette;
		private IDisposable _paletteReset;
		protected int _frameCounter = 0;
		protected long _lastFrame = 0;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected AbstractColorizer(int width, int height, Coloring coloring, Animation[] animations)
		{
			Width = width;
			Height = height;
			Coloring = coloring;
			Animations = animations;
			ColoredFrame = new byte[width * height * 3];
			SetPalette(Coloring.DefaultPalette, true);
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
			Logger.Info("[colorize] Setting palette {0} of {1} colors via {2} frame: [ {3} ]", mapping.PaletteIndex, palette.Colors.Length, masked, string.Join(" ", palette.Colors.Select(c => c.ToString())));
			_paletteReset?.Dispose();
			_paletteReset = null;

			SetPalette(palette);

			switch (mapping.Mode)
			{
				// Numä iifärbä (hemmr scho) und guät isch
				case 0:
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

				// Än Animazion wird losgla
				case 1:
					var animation = Animation.Find(Animations, mapping.Duration);
					if (animation == null) {
						Logger.Warn("[colorize] No animation found at position {0} for {1} frame.", mapping.Duration, masked);
						return false;
					}
					Logger.Info("[colorize] Playing animation of {0} frames via {1} frame.", animation.NumFrames, masked);
					CurrentAnimation?.Stop();
					CurrentEnhancer?.Stop();
					CurrentAnimation = animation;
					CurrentAnimation.Start(AnimationFrames, Palette);
					_frameCounter = 0;
					_lastFrame = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
					return true;

				// Ab etz wärdid d Biudli mit zwe Bit ergänzt
				case 2:
					var enhancer = Animation.Find(Animations, mapping.Duration);
					if (enhancer == null) {
						Logger.Warn("[colorize] No animation found at position {0} for {1} frame.", mapping.Duration, masked);
						return false;
					}
					if (CurrentEnhancer == null || !CurrentEnhancer.Equals(enhancer)) {
						Logger.Info("[colorize] Enhancing animation of {0} frames via {1} frame.", enhancer.NumFrames, masked);
						CurrentAnimation?.Stop();
						CurrentEnhancer?.Stop();
						CurrentEnhancer = enhancer;
						CurrentEnhancer.Start();
					}
					return true;
				
				default:
					Logger.Warn("[colorize] Unknown mode {0}.", mapping.Mode);
					return false;
			}
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

		public IObservable<byte[]> GetRgb24Frames()
		{
			return AnimationFrames;
		}
	}
}
