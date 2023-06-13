using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray6 : AbstractConverter, IColoredGray6Source
	{
		public override string Name => "Converter[Gray6 -> ColoredGray6]";
		public override IEnumerable<FrameFormat> From => new [] {FrameFormat.Gray4};

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray6Frames = new Subject<ColoredFrame>();

		public ConverterGray6(Func<DmdFrame, ColoredFrame> convert) : base(true)
			=> _convert = convert;

		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6Frames;

		public override void Convert(DmdFrame frame) => _coloredGray6Frames.OnNext(_convert(frame));
	}
}
