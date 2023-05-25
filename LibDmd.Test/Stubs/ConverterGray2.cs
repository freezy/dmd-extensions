using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray2 : IConverter, IColoredGray2Source
	{
		public string Name => "Gray2 Converter";
		public BehaviorSubject<Dimensions> Dimensions { get; set; }
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		public FrameFormat From => FrameFormat.Gray2;

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray2Frames = new Subject<ColoredFrame>();

		public ConverterGray2(Func<DmdFrame, ColoredFrame> convert)
		{
			_convert = convert;
		}

		public IObservable<ColoredFrame> GetColoredGray2Frames() => _coloredGray2Frames;

		public void Convert(DmdFrame frame) => _coloredGray2Frames.OnNext(_convert(frame));

		public void Init()
		{
		}

		public void SetDimensions(Dimensions dim)
		{
		}
	}
}
