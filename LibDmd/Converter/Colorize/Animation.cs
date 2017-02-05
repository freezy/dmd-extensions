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
using ProPinballBridge;

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
		public Frame LastFrame => _frames[_frames.Length - 1];

		private readonly Frame[] _frames;
		private readonly IObservable<Frame> _fsqFrames;
		private BehaviorSubject<byte[][]> _vpmFrames;
		private IDisposable _animation;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Kreiärt ä nii Animazion
		/// </summary>
		/// <param name="reader">S Feil wod Animazion dinnä staht</param>
		public Animation(BinaryReader reader)
		{
			Offset = reader.BaseStream.Position;
			var numFrames = reader.ReadUInt16BE();
			//Logger.Trace("  [{1}] [fsq] Reading {0} frames", numFrames, reader.BaseStream.Position);
			_frames = new Frame[numFrames];
			uint time = 0;
			for (var i = 0; i < numFrames; i++) {
				_frames[i] = new Frame(reader, time);
				time += _frames[i].Delay;
			}
			_fsqFrames = _frames
				.ToObservable()
				.Delay(frame => Observable.Timer(TimeSpan.FromMilliseconds(frame.Time)));
		}

		/// <summary>
		/// Tuät d Animazion looslah und d Biudli uif diä entschprächendi Queuä
		/// uisgäh.
		/// </summary>
		/// <remarks>
		/// Das hiä isch dr Fau wo diä gsamti Animazion uisgäh und VPM ignoriärt
		/// wird (dr Modus eis).
		/// </remarks>
		/// <param name="coloredGray2Source">Wenn meglich gahts da druif</param>
		/// <param name="coloredGray4Source">Wenns viärbittig isch, de wird dä zersch probiärt</param>
		/// <param name="palette">D Palettä wo zum iifärbä bruicht wird</param>
		/// <param name="completed">Wird uisgfiärt wenn fertig</param>
		public void StartReplace(Subject<Tuple<byte[][], Color[]>> coloredGray2Source, Subject<Tuple<byte[][], Color[]>> coloredGray4Source, BehaviorSubject<Palette> palette, Action completed = null)
		{
			Logger.Info("[fsq] Starting colored gray4 animation of {0} frames...", _frames.Length);
			IsRunning = true;
			var t = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var n = 0;
			_animation = _fsqFrames
				.Subscribe(frame => {
					//Logger.Trace("[timing] FSQ Frame #{0} played ({1} ms, theory: {2} ms).", n, (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - t, _frames[n].Time);
					if (frame.BitLength == 2) {
						coloredGray2Source.OnNext(new Tuple<byte[][], Color[]>(frame.Planes, palette.Value.GetColors(frame.BitLength)));
					} else {
						coloredGray4Source.OnNext(new Tuple<byte[][], Color[]>(frame.Planes, palette.Value.GetColors(frame.BitLength)));
					}
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
		/// Tuät d Animazion looslah und d Biudli uifd viärbit-Queuä uisgäh
		/// </summary>
		/// <remarks>
		/// Das hiä isch dr Fau wo Buider vo VPM mit zwe Bits erwiiterid wärdid.
		/// 
		/// S Timing wird wiä im Modus eis vo dr Animazion vorgäh, das heisst s 
		/// letschtä Biud vo VPM definiärt diä erschtä zwäi Bits unds jedes Biud
		/// vord Animazion tuät diä reschtlichä zwäi Bits ergänzä unds de uifd
		/// Viärbit-Queuä uisgäh.
		/// </remarks>
		/// <param name="firstFrame">S Buid vo VPM wod Animazion losgla het</param>
		/// <param name="coloredGray4Source">D Uisgab vord erwiitertä Frames</param>
		/// <param name="palette">D Palettä wo zum iifärbä bruicht wird</param>
		/// <param name="completed">Wird uisgfiärt wenn fertig</param>
		public void StartEnhance(byte[][] firstFrame, Subject<Tuple<byte[][], Color[]>> coloredGray4Source, BehaviorSubject<Palette> palette, Action completed = null)
		{
			Logger.Info("[fsq] Starting enhanced animation of {0} frames...", _frames.Length);
			IsRunning = true;
			var t = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			var n = 0;
			_vpmFrames = new BehaviorSubject<byte[][]>(firstFrame);
			_animation = _fsqFrames
				.Select(fsqFrame => new []{ _vpmFrames.Value[0], _vpmFrames.Value[1], fsqFrame.Planes[0], fsqFrame.Planes[1] })
				.Subscribe(planes => {
					//Logger.Trace("[timing] FSQ enhanced Frame #{0} played ({1} ms, theory: {2} ms).", n, (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - t, _frames[n].Time);
					coloredGray4Source.OnNext(new Tuple<byte[][], Color[]>(planes, palette.Value.GetColors(planes.Length)));
					n++;
				}, () => {
					//Logger.Trace("[timing] Last frame enhanced, waiting {0}ms for last frame to finish playing.", _frames[_frames.Length - 1].Delay);

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
		/// Tuäts nächschti Buid vo VPM setzä wos de mitr laifendä Animazion
		/// ergänzt wird.
		/// </summary>
		/// <remarks>
		/// Aui Buidr minnd zwäi Bit sii.
		/// </remarks>
		/// <param name="planes">S zwäibittigä Biud, aus Bitplanes</param>
		public void NextVpmFrame(byte[][] planes)
		{
			_vpmFrames?.OnNext(planes);
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
		/// <returns></returns>
		public static Animation[] ReadFrameSequence(string filename)
		{
			var fs = new FileStream(filename, FileMode.Open);
			var reader = new BinaryReader(fs);
			var numAnimations = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [fsq] Reading {0} animations", numAnimations, reader.BaseStream.Position);
			var animations = new Animation[numAnimations];
			for (var i = 0; i < numAnimations; i++) {
				animations[i] = new Animation(reader);
			}
			return animations;
		}
	}
}
