using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
	/// Im Modus eis chemid aui Biudr vo <see cref="Frames"/>. Fird Uisgab wird
	/// VPM ignoriärt. S Timing wird ibr <see cref="Frame.Delay"/> definiärt.
	/// 
	/// Im Modus zwäi chemid d Biudr vo VPM. Fir d Uisgab wärdid d Bits vo
	/// <see cref="Frames"/> a diä bestehendä Datä hinnä anäghänkt. S Timing 
	/// bliibt s gliichä wiä das vo VPM.
	/// </remarks>
	public class Animation
	{
		public readonly Frame[] Frames;

		public bool IsRunning { get; private set; }

		private readonly int _width;
		private readonly int _height;
		private IDisposable _animation;
		private uint _currentFrame;

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
			Frames = new Frame[numFrames];
			uint time = 0;
			for (var i = 0; i < numFrames; i++) {
				Frames[i] = new Frame(reader, time);
				time += Frames[i].Delay;
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
			Logger.Info("[fsq] Starting animation of {0} frames...", Frames.Length);
			IsRunning = true;
			_animation = Frames.ToObservable()
				.Delay(frame => Observable.Timer(TimeSpan.FromMilliseconds(frame.Time)))
				.Select(frame => frame.GetFrame(_width, _height))
				.Select(frame => ColorUtil.ColorizeFrame(_width, _height, frame, palette.Value))
				.Subscribe(frameSource.OnNext, () => { IsRunning = false; });
		}
		
		public void Start()
		{
			_currentFrame = 0;
			IsRunning = true;
		}

		public Frame Next()
		{
			if (!IsRunning) {
				throw new InvalidOperationException("Cannot retrieve next frame of stopped animation.");
			}
			if (Frames.Length == _currentFrame + 1) {
				IsRunning = false;
			}
			return Frames[_currentFrame++];
		}

		public void Stop()
		{
			_animation?.Dispose();
			IsRunning = false;
		}

		/// <summary>
		/// Tuät aui Animazionä vom Feil inälääsä.
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
