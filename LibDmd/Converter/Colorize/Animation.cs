using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

		public void Start(Subject<byte[]> frameSource, BehaviorSubject<Color[]> palette)
		{
			Logger.Info("[fsq] Starting animation of {0} frames...", Frames.Length);
			IsRunning = true;

			var delays = Frames.ToObservable().Select(frame => Observable.Timer(TimeSpan.FromMilliseconds(frame.Delay)));
			//var delays = Observable.Interval(TimeSpan.FromMilliseconds(250)).Delay(TimeSpan.FromSeconds(2));
			var frames = Frames.ToObservable();

			frames.Zip(delays.Switch(), (l, r) => l).Subscribe(frame => {
				Logger.Info("[fsq] Playing {0}-bit frame for {1}ms...", frame.BitLength, frame.Delay);
				frameSource.OnNext(ColorUtil.ColorizeFrame(_width, _height, frame.GetFrame(_width, _height), palette.Value));
			}, () => {
				Logger.Info("[fsq] Animation done.");
				IsRunning = false;
			});

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
