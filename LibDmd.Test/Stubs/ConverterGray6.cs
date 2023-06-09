using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray6 : AbstractConverter, IColoredGray6Source
	{
		public override string Name => "Converter[Gray6]";
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		public override IEnumerable<FrameFormat> From => new [] {FrameFormat.Gray4};

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray6Frames = new Subject<ColoredFrame>();

		public ConverterGray6(Func<DmdFrame, ColoredFrame> convert)
		{
			_convert = convert;
		}

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6Frames;

		public override void Convert(DmdFrame frame) => _coloredGray6Frames.OnNext(_convert(frame));
		public override void Convert(AlphaNumericFrame frame) { }


		public void SetDimensions(Dimensions dim)
		{
		}
	}
}
