using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Common;
using NLog;

namespace LibDmd.Converter.Colorize
{
	/// <summary>
	/// Animazionsdatä wo entweder ä Animazion uisem ROM komplett uistuischid
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
			var numFrames = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [fsq] Reading {0} frames", numFrames, reader.BaseStream.Position);
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
		public void Start(Subject<byte[]> frameSource, BehaviorSubject<Color[]> palette)
		{
			Logger.Info("[fsq] Starting animation of {0} frames...", _frames.Length);
			IsRunning = true;
			_animation = _frames.ToObservable()
				.Delay(frame => Observable.Timer(TimeSpan.FromMilliseconds(frame.Time)))
				.Select(frame => frame.GetFrame(_width, _height))
				.Select(frame => ColorUtil.ColorizeFrame(_width, _height, frame, palette.Value))
				.Subscribe(frameSource.OnNext, () => { IsRunning = false; });
		}

		/// <summary>
		/// Tuät d Animazion aus loosglah markiäre. Äs wird abr nid uisgäh.
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
