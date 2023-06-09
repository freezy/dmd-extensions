using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter
{
	/// <summary>
	/// This is a wrapper source around a converter, that sends frames in a regular interval to the converter, de-dupes
	/// the convert's output, and emits the de-duped frames to the output sources.
	/// </summary>
	public class ClockedDeduper : IColoredGray2Source, IColoredGray4Source, IColoredGray6Source, IRgb24Source, IAlphaNumericSource, IDisposable
	{
		private const int ClockFps = 60;

		public string Name => ((ISource)_converter).Name;

		public IObservable<ColoredFrame> GetColoredGray2Frames() => _coloredGray2Frames;
		public IObservable<ColoredFrame> GetColoredGray4Frames() => _coloredGray4Frames;
		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6Frames;
		public IObservable<DmdFrame> GetRgb24Frames() => _rgb24Frames;
		public IObservable<AlphaNumericFrame> GetAlphaNumericFrames() => _alphaNumFrames;

		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;

		private readonly AbstractConverter _converter;
		private readonly CompositeDisposable _activeSources = new CompositeDisposable();

		private readonly Subject<ColoredFrame> _coloredGray2Frames = new Subject<ColoredFrame>();
		private readonly Subject<ColoredFrame> _coloredGray4Frames = new Subject<ColoredFrame>();
		private readonly Subject<ColoredFrame> _coloredGray6Frames = new Subject<ColoredFrame>();
		private readonly Subject<DmdFrame> _rgb24Frames = new Subject<DmdFrame>();
		private readonly Subject<AlphaNumericFrame> _alphaNumFrames = new Subject<AlphaNumericFrame>();
		private readonly IDisposable _clock;

		private DmdFrame _lastDmdFrame;
		private AlphaNumericFrame _lastAlphanumFrame;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ClockedDeduper(AbstractConverter converter)
		{
			if (converter == null) {
				return;
			}

			// _converter = converter;
			// var ac = converter as AbstractConverter;
			// if (_converter is IColoredGray2Source coloredGray2Source && ac != null && !ac.IsConnected) {
			// 	_activeSources.Add(coloredGray2Source.GetColoredGray2Frames().DistinctUntilChanged().Subscribe(_coloredGray2Frames));
			// }
			// if (_converter is IColoredGray4Source coloredGray4Source && ac != null && !ac.IsConnected) {
			// 	_activeSources.Add(coloredGray4Source.GetColoredGray4Frames().DistinctUntilChanged().Subscribe(_coloredGray4Frames));
			// }
			// if (_converter is IColoredGray6Source coloredGray6Source && ac != null && !ac.IsConnected) {
			// 	_activeSources.Add(coloredGray6Source.GetColoredGray6Frames().DistinctUntilChanged().Subscribe(_coloredGray6Frames));
			// }
			// if (_converter is IRgb24Source rgb24Source && ac != null && !ac.IsConnected) {
			// 	_activeSources.Add(rgb24Source.GetRgb24Frames().DistinctUntilChanged().Subscribe(_rgb24Frames));
			// }

			// _clock = Observable
			// 	.Interval(TimeSpan.FromSeconds(1d / ClockFps))
			// 	.Subscribe(Tick);
		}

		public void Convert(DmdFrame frame)
		{
			Logger.Info($"*** New Frame {frame.Format}");
			_converter.Convert(frame);
			_lastDmdFrame = frame;
		}

		public void Convert(AlphaNumericFrame frame)
		{
			_lastAlphanumFrame = frame;

			// passthrough
			_alphaNumFrames.OnNext(frame);
		}

		private void Tick(long _)
		{
			if (_lastDmdFrame != null) {
				Logger.Info("*** New Tick");
				_converter.Convert(_lastDmdFrame);
			}

			if (_lastAlphanumFrame != null) {
				_converter.Convert(_lastAlphanumFrame);
			}
		}

		public void Dispose()
		{
			_activeSources?.Dispose();
			_coloredGray2Frames?.Dispose();
			_coloredGray4Frames?.Dispose();
			_coloredGray6Frames?.Dispose();
			_rgb24Frames?.Dispose();
			_clock?.Dispose();
		}
	}
}
