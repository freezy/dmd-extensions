using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Animation(BinaryReader reader, int width, int height)
		{
			_width = width;
			_height = height;
			var numFrames = reader.ReadUInt16BE();
			Logger.Trace("  [{1}] [fsq] Reading {0} frames", numFrames, reader.BaseStream.Position);
			Frames = new Frame[numFrames];
			for (var i = 0; i < numFrames; i++) {
				Frames[i] = new Frame(reader);
			}
		}

		public void Start(Subject<byte[]> frameSource)
		{
			IsRunning = true;
			Frames.ToObservable()
				.Where(frame => IsRunning)
				.Delay(frame => Observable.Timer(TimeSpan.FromMilliseconds(frame.Delay)))
				.Select(frame => frame.GetFrame(_width, _height))
				.Subscribe(frameSource.OnNext, () => IsRunning = false);
		}

		public void Stop()
		{
			IsRunning = false;
		}

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
