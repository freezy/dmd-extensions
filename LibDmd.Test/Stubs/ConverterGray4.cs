using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray4 : AbstractConverter, IColoredGray4Source
	{
		public override string Name => "Converter[Gray4]";

		public override IEnumerable<FrameFormat> From => new [] {FrameFormat.Gray4};

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray4Frames = new Subject<ColoredFrame>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ConverterGray4(Func<DmdFrame, ColoredFrame> convert)
		{
			_convert = convert;
		}

		public IObservable<ColoredFrame> GetColoredGray4Frames() => _coloredGray4Frames;

		public override void Convert(DmdFrame frame)
		{
			Logger.Info($"ConverterGray4.Convert");
			_coloredGray4Frames.OnNext(_convert(frame));
		}

		public void Init()
		{
		}
	}
}
