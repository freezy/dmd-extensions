using System;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray4 : IConverter, IColoredGray4Source
	{
		public string Name => "Gray4 Converter";
		public BehaviorSubject<Dimensions> Dimensions { get; set; }
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		public FrameFormat From => FrameFormat.Gray4;

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray4Frames = new Subject<ColoredFrame>();

		public ConverterGray4(Func<DmdFrame, ColoredFrame> convert)
		{
			_convert = convert;
		}

		public IObservable<ColoredFrame> GetColoredGray4Frames() => _coloredGray4Frames;

		public void Convert(DmdFrame frame) => _coloredGray4Frames.OnNext(_convert(frame));

		public void Init()
		{
		}

		public void SetDimensions(Dimensions dim)
		{
		}
	}
}
