using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray2 : AbstractConverter, IColoredGray2Source
	{
		public override string Name => "Converter[Gray2 -> ColoredGray2]";
		public override IEnumerable<FrameFormat> From => new[] { FrameFormat.Gray2 };

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray2Frames = new Subject<ColoredFrame>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ConverterGray2(Func<DmdFrame, ColoredFrame> convert) => _convert = convert;

		public IObservable<ColoredFrame> GetColoredGray2Frames() => _coloredGray2Frames;

		public override void Convert(DmdFrame frame) => _coloredGray2Frames.OnNext(_convert(frame));
	}
}
