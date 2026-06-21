using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter
{
	/// <summary>
	/// Converter that can swap in another child converter dynamically, without requiring upstream subscribers to re-subscribe.
	/// Falls back to orange DMD colored frames when no child converter is available.
	/// </summary>
	public class SwitchingConverter : AbstractConverter, IColoredGray2Source, IColoredGray4Source, IColoredGray6Source,
		IRgb565Source, IRgb24Source, IAlphaNumericSource
	{
		private AbstractConverter _converter;
		private readonly Subject<ColoredFrame> _coloredGray2PassthroughFrames = new Subject<ColoredFrame>();
		private readonly Subject<ColoredFrame> _coloredGray4PassthroughFrames = new Subject<ColoredFrame>();
		private readonly Subject<AlphaNumericFrame> _alphaNumericPassthroughFrames = new Subject<AlphaNumericFrame>();
		private Color _color = Colors.OrangeRed;

		private readonly ReplaySubject<IObservable<ColoredFrame>> _latestColoredGray2 = new ReplaySubject<IObservable<ColoredFrame>>(1);
		private readonly ReplaySubject<IObservable<ColoredFrame>> _latestColoredGray4 = new ReplaySubject<IObservable<ColoredFrame>>(1);
		private readonly ReplaySubject<IObservable<ColoredFrame>> _latestColoredGray6 = new ReplaySubject<IObservable<ColoredFrame>>(1);
		private readonly ReplaySubject<IObservable<DmdFrame>> _latestRgb565 = new ReplaySubject<IObservable<DmdFrame>>(1);
		private readonly ReplaySubject<IObservable<DmdFrame>> _latestRgb24 = new ReplaySubject<IObservable<DmdFrame>>(1);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public override string Name => $"Switching Converter ({ConverterName(_converter)})";

		public override IEnumerable<FrameFormat> From => new[] { FrameFormat.Gray2, FrameFormat.Gray4, FrameFormat.AlphaNumeric };

		public override bool Supports(FrameFormat format) => true;

		public SwitchingConverter() : base(false)
		{
			_latestColoredGray2.OnNext(_coloredGray2PassthroughFrames);
			_latestColoredGray4.OnNext(_coloredGray4PassthroughFrames);
			_latestColoredGray6.OnNext(Observable.Empty<ColoredFrame>());
			_latestRgb565.OnNext(Observable.Empty<DmdFrame>());
			_latestRgb24.OnNext(Observable.Empty<DmdFrame>());
		}

		public override void Convert(DmdFrame frame)
		{
			if (_converter != null) {
				_converter?.Convert(frame);
			} else {
				if (frame.BitLength == 4) {
					_coloredGray4PassthroughFrames.OnNext(new ColoredFrame(frame, _color));
				} else {
					_coloredGray2PassthroughFrames.OnNext(new ColoredFrame(frame, _color));
				}
			}
		}

		public override void Convert(AlphaNumericFrame frame)
		{
			if (_converter != null) {
				_converter?.Convert(frame);
			} else {
				_alphaNumericPassthroughFrames.OnNext(frame);
			}
		}

		public IObservable<ColoredFrame> GetColoredGray2Frames() => _latestColoredGray2.Switch();

		public IObservable<ColoredFrame> GetColoredGray4Frames() => _latestColoredGray4.Switch();

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _latestColoredGray6.Switch();

		public IObservable<DmdFrame> GetRgb565Frames() => _latestRgb565.Switch();

		public IObservable<DmdFrame> GetRgb24Frames() => _latestRgb24.Switch();

		public IObservable<AlphaNumericFrame> GetAlphaNumericFrames() => _alphaNumericPassthroughFrames;

		public void Switch(AbstractConverter converter)
		{
			Logger.Info($"{Name} switching to {ConverterName(converter)}");

			if (converter == null) {
				_latestColoredGray2.OnNext(_coloredGray2PassthroughFrames);
				_latestColoredGray4.OnNext(_coloredGray4PassthroughFrames);
				_latestColoredGray6.OnNext(Observable.Empty<ColoredFrame>());
				_latestRgb565.OnNext(Observable.Empty<DmdFrame>());
				_latestRgb24.OnNext(Observable.Empty<DmdFrame>());
				_converter = null;
				return;
			}

			_latestColoredGray2.OnNext(converter is IColoredGray2Source source2
				? source2.GetColoredGray2Frames()
				: Observable.Empty<ColoredFrame>());
			_latestColoredGray4.OnNext(converter is IColoredGray4Source source4
				? source4.GetColoredGray4Frames()
				: Observable.Empty<ColoredFrame>());
			_latestColoredGray6.OnNext(converter is IColoredGray6Source source6
				? source6.GetColoredGray6Frames()
				: Observable.Empty<ColoredFrame>());
			_latestRgb565.OnNext(converter is IRgb565Source sourceRgb565
				? sourceRgb565.GetRgb565Frames()
				: Observable.Empty<DmdFrame>());
			_latestRgb24.OnNext(converter is IRgb24Source sourceRgb24
				? sourceRgb24.GetRgb24Frames()
				: Observable.Empty<DmdFrame>());

			_converter = converter;
		}

		/// <summary>
		/// Sets the color with which a grayscale source is rendered when no converter is set.
		/// </summary>
		/// <param name="color">Rendered color</param>
		public void SetColor(Color color)
		{
			_color = color;
		}

		private static string ConverterName(AbstractConverter converter)
		{
			if (converter is AbstractSource source) {
				return source.Name;
			}

			return "Passthrough";
		}
	}
}
