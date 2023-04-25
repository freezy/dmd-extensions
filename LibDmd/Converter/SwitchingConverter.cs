using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Media;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter
{
	/// <summary>
	/// Converter that can swap in another child converter dynamically, without requiring upstream subscribers to re-subscribe.
	/// Falls back to orange DMD colored frames when no child converter is available.
	/// </summary>
	public class SwitchingConverter : AbstractSource, IConverter, IColoredGray6Source, IColoredGraySource
	{
		private IConverter _converter;
		private readonly Subject<ColoredFrame> _coloredGray2PassthroughFrames = new Subject<ColoredFrame>();
		private readonly Subject<ColoredFrame> _coloredGray4PassthroughFrames = new Subject<ColoredFrame>();
		private Color _color = Colors.OrangeRed;

		private readonly ReplaySubject<IObservable<ColoredFrame>> _latestColoredGray6 = new ReplaySubject<IObservable<ColoredFrame>>(1);
		private readonly ReplaySubject<IObservable<ColoredFrame>> _latestColoredGray = new ReplaySubject<IObservable<ColoredFrame>>(1);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public override string Name => $"Switching Converter ({ConverterName(_converter)})";

		public FrameFormat From { get; }
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }

		public SwitchingConverter(FrameFormat frameFormat)
		{
			From = frameFormat;
			_latestColoredGray6.OnNext(Observable.Empty<ColoredFrame>());
			_latestColoredGray.OnNext(Observable.Empty<ColoredFrame>());
		}

		public void Convert(DMDFrame frame)
		{
			if (_converter != null) {
				_converter?.Convert(frame);
			} else {
				if (frame.BitLength == 4) {
					_coloredGray4PassthroughFrames.OnNext(new ColoredFrame(frame.width, frame.height, frame.Data, _color));
				} else {
					_coloredGray2PassthroughFrames.OnNext(new ColoredFrame(frame.width, frame.height, frame.Data, _color));
				}
			}
		}

		public void Convert(AlphaNumericFrame frame)
		{
			
		}

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _latestColoredGray6.Switch();

		public IObservable<ColoredFrame> GetColoredGrayFrames() => _latestColoredGray.Switch();

		public void Init()
		{
		}

		public void Switch(IConverter converter)
		{
			Logger.Info($"{Name} switching to {ConverterName(converter)}");

			if (converter == null) {
				_latestColoredGray6.OnNext(Observable.Empty<ColoredFrame>());
				_latestColoredGray.OnNext(Observable.Empty<ColoredFrame>());
				_converter = null;
				return;
			}

			converter.Init();

			if (converter is IColoredGray6Source source6) {
				source6.Dimensions = Dimensions;
				_latestColoredGray6.OnNext(source6.GetColoredGray6Frames());
			}

			if (converter is IColoredGraySource source24) {
				source24.Dimensions = Dimensions;
				_latestColoredGray.OnNext(source24.GetColoredGrayFrames());
			}

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

		private static string ConverterName(IConverter converter)
		{
			if (converter is AbstractSource source) {
				return source.Name;
			}

			return "Passthrough";
		}
	}
}
