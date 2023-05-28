using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray6 : IConverter, IColoredGray6Source
	{
		public string Name => "Gray6 Converter";
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		public FrameFormat From => FrameFormat.Gray4;

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray6Frames = new Subject<ColoredFrame>();

		public ConverterGray6(Func<DmdFrame, ColoredFrame> convert)
		{
			_convert = convert;
		}

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6Frames;

		public void Convert(DmdFrame frame) => _coloredGray6Frames.OnNext(_convert(frame));

		public void Init()
		{
		}

		public void SetDimensions(Dimensions dim)
		{
		}
	}
}
