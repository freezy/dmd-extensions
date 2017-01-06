using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media;
using System.Windows.Navigation;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Colorize
{
	/// <summary>
	/// Animazionsdatä wo entweder än Animazion uisem ROM komplett uistuischid
	/// odr abr erwiiterid.
	/// </summary>
	/// 
	/// <remarks>
	/// Än Animazion wird abgschpiut wenns äs Matching git und dr Modus eis odr
	/// zwei isch.
	/// 
	/// Im Modus eis chemid aui Biudr vo <see cref="_frames"/>. Fird Uisgab wird
	/// VPM ignoriärt. S Timing wird ibr <see cref="Frame.Delay"/> definiärt.
	/// 
	/// Im Modus zwäi chemid d Biudr vo VPM. Fir d Uisgab wärdid d Bits vo
	/// <see cref="_frames"/> a diä bestehendä Datä hinnä anäghänkt. S Timing 
	/// bliibt s gliichä wiä das vo VPM.
	/// </remarks>
	public class Animation
	{
		public bool IsRunning { get; private set; }
		public readonly long Offset;
		public int NumFrames => _frames.Length;

		private readonly Frame[] _frames;
		private readonly int _width;
		private readonly int _height;
		private IDisposable _animation;

		private uint _frameIndex;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Kreiärt ä nii Animazion
		/// </summary>
		/// <param name="reader">S Feil wod Animazion dinnä staht</param>
		/// <param name="width">Bräiti vom Biud</param>
		/// <param name="height">Heechi vom Biud</param>
		public Animation(BinaryReader reader, int width, int height)
		{
			_width = width;
			_height = height;
			Offset = reader.BaseStream.Position;
			var numFrames = reader.ReadUInt16BE();
			//Logger.Trace("  [{1}] [fsq] Reading {0} frames", numFrames, reader.BaseStream.Position);
			_frames = new Frame[numFrames];
			uint time = 0;
			for (var i = 0; i < numFrames; i++) {
				_frames[i] = new Frame(reader, time);
				time += _frames[i].Delay;
			}
		}

		/// <summary>
		/// Tuät d Animazion looslah und d Biudli uif diä entschprächendi Queuä
		/// uisgäh.
		/// </summary>
		/// 
		/// <remarks>
		/// Das hiä isch dr Fau wo diä gsamti Animazion uisgäh und VPM ignoriärt
		/// wird (dr Modus eis).
		/// </remarks>
		/// <param name="frameSource">Det wärdid Biudli uisgäh</param>
		/// <param name="palette">D Palettä wo zum iifärbä bruicht wird</param>
		/// <param name="completed">Wird uisgfiärt wenn fertig</param>
		public void Start(Subject<byte[]> frameSource, BehaviorSubject<Palette> palette, Action completed = null)
		{
			Logger.Info("[fsq] Starting RGB24 animation of {0} frames...", _frames.Length);
			IsRunning = true;
			var n = 0;
			var t = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			_animation = _frames.ToObservable()
				.Delay(frame => Observable.Timer(TimeSpan.FromMilliseconds(frame.Time)))
				.Select(frame => ColorUtil.ColorizeFrame(_width, _height, frame.GetFrame(_width, _height), palette.Value.GetColors(frame.BitLength)))
				.Subscribe(frame => {
					frameSource.OnNext(frame);
					//Logger.Trace("[timing] FSQ Frame #{0} played ({1} ms, theory: {2} ms).", n, (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - t, _frames[n].Time);
					n++;
				}, () => {

					// nu uifs letschti biud wartä bis mer fertig sind
					Observable
						.Never<Unit>()
						.StartWith(Unit.Default)
						.Delay(TimeSpan.FromMilliseconds(_frames[_frames.Length - 1].Delay))
						.Subscribe(_ => {
							IsRunning = false;
							completed?.Invoke();
						});
				});
		}

		/// <summary>
		/// Tuät d Animazion looslah und d Biudli uif diä entschprächendi Queuä
		/// uisgäh, abr etz aus Bitplanes.
		/// </summary>
		/// <param name="coloredGray2Source">Wenn meglich gahts da druif</param>
		/// <param name="coloredGray4Source">Wenns viärbittig isch, de wird dä zersch probiärt</param>
		/// <param name="palette">D Palettä wo zum iifärbä bruicht wird</param>
		/// <param name="completed">Wird uisgfiärt wenn fertig</param>
		public void Start(Subject<Tuple<byte[][], Color[]>> coloredGray2Source, Subject<Tuple<byte[][], Color[]>> coloredGray4Source, BehaviorSubject<Palette> palette, Action completed = null)
		{
			Logger.Info("[fsq] Starting colored gray4 animation of {0} frames...", _frames.Length);
			IsRunning = true;
			var t = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			_animation = _frames.ToObservable()
				.Delay(frame => Observable.Timer(TimeSpan.FromMilliseconds(frame.Time)))
				.Subscribe(frame => {
					if (frame.BitLength == 2) {
						coloredGray2Source.OnNext(new Tuple<byte[][], Color[]>(frame.Planes, palette.Value.GetColors(frame.BitLength)));
					} else {
						coloredGray4Source.OnNext(new Tuple<byte[][], Color[]>(frame.Planes, palette.Value.GetColors(frame.BitLength)));
					}
				}, () => {

					// nu uifs letschti biud wartä bis mer fertig sind
					Observable
						.Never<Unit>()
						.StartWith(Unit.Default)
						.Delay(TimeSpan.FromMilliseconds(_frames[_frames.Length - 1].Delay))
						.Subscribe(_ => {
							IsRunning = false;
							completed?.Invoke();
						});
				});
		}


		/// <summary>
		/// Tuät d Animazion aus loosglah markiäre. Äs wird abr niit uisgäh.
		/// </summary>
		/// 
		/// <remarks>
		/// Das hiä isch dr Fau wo zur Bit-Erwiiterig diänt (Modus zwäi). Dr 
		/// Konvärtr chunnt säuber ibr <see cref="Next"/> d Biudr go abholä
		/// bisses käni me hett odr d Animazion gschtoppt wird.
		/// </remarks>
		public void Start()
		{
			_frameIndex = 0;
			IsRunning = true;
		}

		/// <summary>
		/// Gits nächschtä Biud zrugg.
		/// </summary>
		/// <returns>S nächschtä Biud idr Animazion</returns>
		public Frame Next()
		{
			if (!IsRunning) {
				throw new InvalidOperationException("Cannot retrieve next frame of stopped animation.");
			}
			if (_frames.Length == _frameIndex + 1) {
				IsRunning = false;
			}
			return _frames[_frameIndex++];
		}

		/// <summary>
		/// Tuät d Animazion aahautä.
		/// </summary>
		public void Stop()
		{
			_animation?.Dispose();
			IsRunning = false;
		}

		public bool Equals(Animation animation)
		{
			return Offset == animation.Offset;
		}

		/// <summary>
		/// Findet än Animazion wo anärä gegäbänä Steu im Feil gsi isch.
		/// </summary>
		/// <param name="animations">D Lischtä vo aunä Animazionä</param>
		/// <param name="offset">D Steu im Feil</param>
		/// <returns>Diä gfundini Animazion odr sisch null</returns>
		public static Animation Find(Animation[] animations, long offset)
		{
			// TODO index bruichä
			return animations.FirstOrDefault(animation => animation.Offset == offset);
		}

		/// <summary>
		/// Tuät aui Animazionä vom Feil uisälääsä.
		/// </summary>
		/// <param name="filename">Dr Pfad zum Feil</param>
		/// <param name="width">Bräiti vom Biud</param>
		/// <param name="height">Heechi vom Biud</param>
		/// <returns></returns>
		public static Animation[] ReadFrameSequence(string filename, int width, int height)
		{
			var fs = new FileStream(filename, FileMode.Open);
			var reader = new BinaryReader(fs);
			var numAnimations = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [fsq] Reading {0} animations", numAnimations, reader.BaseStream.Position);
			var animations = new Animation[numAnimations];
			for (var i = 0; i < numAnimations; i++) {
				animations[i] = new Animation(reader, width, height);
			}
			return animations;
		}
	}
}
