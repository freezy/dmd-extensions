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
	public class SwitchingConverter : AbstractSource, IConverter, IColoredGray2Source, IColoredGray4Source, IColoredGray6Source
	{
		private IConverter converter;
		private readonly Subject<ColoredFrame> ColoredGray2AnimationFrames = new Subject<ColoredFrame>();

		private readonly ReplaySubject<IObservable<ColoredFrame>> LatestColoredGray2 = new ReplaySubject<IObservable<ColoredFrame>>(1);
		private readonly ReplaySubject<IObservable<ColoredFrame>> LatestColoredGray4 = new ReplaySubject<IObservable<ColoredFrame>>(1);
		private readonly ReplaySubject<IObservable<ColoredFrame>> LatestColoredGray6 = new ReplaySubject<IObservable<ColoredFrame>>(1);

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public override string Name
		{
			get
			{
				return $"Switching Converter ({ConverterName(converter)})";
			}
		}

		public FrameFormat From { get; } = FrameFormat.Gray2;
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }

		public SwitchingConverter()
		{
			LatestColoredGray2.OnNext(ColoredGray2AnimationFrames);
			LatestColoredGray4.OnNext(Observable.Empty<ColoredFrame>());
			LatestColoredGray6.OnNext(Observable.Empty<ColoredFrame>());
		}

		public void Convert(DMDFrame frame)
		{
			if (converter != null)
			{
				converter?.Convert(frame);
			}
			else
			{
				ColoredGray2AnimationFrames.OnNext(new ColoredFrame(frame.width, frame.height, frame.Data, Color.FromRgb(0xff, 0x66, 0x00)));
			}
		}

		public IObservable<ColoredFrame> GetColoredGray2Frames()
		{
			return LatestColoredGray2.Switch();
		}

		public IObservable<ColoredFrame> GetColoredGray4Frames()
		{
			return LatestColoredGray4.Switch();
		}

		public IObservable<ColoredFrame> GetColoredGray6Frames()
		{
			return LatestColoredGray6.Switch();
		}

		public void Init()
		{
		}

		public void Switch(IConverter converter)
		{
			Logger.Info($"{Name} switching to {ConverterName(converter)}");

			if (converter == null)
			{
				LatestColoredGray2.OnNext(ColoredGray2AnimationFrames);
				LatestColoredGray4.OnNext(Observable.Empty<ColoredFrame>());
				LatestColoredGray6.OnNext(Observable.Empty<ColoredFrame>());
				this.converter = null;
				return;
			}

			converter.Init();

			var source2 = converter as IColoredGray2Source;
			if (source2 != null)
			{
				source2.Dimensions = Dimensions;
				LatestColoredGray2.OnNext(source2.GetColoredGray2Frames());
			}

			var source4 = converter as IColoredGray4Source;
			if (source4 != null)
			{
				source4.Dimensions = Dimensions;
				LatestColoredGray4.OnNext(source4.GetColoredGray4Frames());
			}

			var source6 = converter as IColoredGray6Source;
			if (source6 != null)
			{
				source6.Dimensions = Dimensions;
				LatestColoredGray6.OnNext(source6.GetColoredGray6Frames());
			}

			this.converter = converter;
		}

		private static string ConverterName(IConverter converter)
		{
			var source = converter as AbstractSource;
			if (source != null)
			{
				return source.Name;
			}
			else
			{
				return "Passthrough";
			}
		}
	}
}
