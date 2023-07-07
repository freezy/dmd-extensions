using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray2Multi : AbstractConverter, IColoredGray2Source, IColoredGray4Source, IColoredGray6Source, IRgb24Source
	{
		public override string Name => "Converter[Gray2 -> Multi]";

		public override IEnumerable<FrameFormat> From => new[] { FrameFormat.Gray2 };

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray2Frames = new Subject<ColoredFrame>();
		private readonly Subject<ColoredFrame> _coloredGray4Frames = new Subject<ColoredFrame>();
		private readonly Subject<ColoredFrame> _coloredGray6Frames = new Subject<ColoredFrame>();
		private readonly Subject<DmdFrame> _rgb24Frames = new Subject<DmdFrame>();

		public ConverterGray2Multi(Func<DmdFrame, ColoredFrame> convert) : base(true)
			=> _convert = convert;

		public override bool Supports(FrameFormat format) => true;

		public IObservable<ColoredFrame> GetColoredGray2Frames() => _coloredGray2Frames;
		public IObservable<ColoredFrame> GetColoredGray4Frames() => _coloredGray4Frames;
		public IObservable<ColoredFrame> GetColoredGray6Frames() => _coloredGray6Frames;
		public IObservable<DmdFrame> GetRgb24Frames() => _rgb24Frames;

		public override void Convert(DmdFrame frame)
		{
			var coloredFrame = _convert(frame);
			switch (coloredFrame.BitLength) {
				case 2:
					_coloredGray2Frames.OnNext(coloredFrame);
					break;
				case 4:
					_coloredGray4Frames.OnNext(coloredFrame);
					break;
				case 6:
					_coloredGray6Frames.OnNext(coloredFrame);
					break;
				default:
					throw new ArgumentException();
			}
		}
	}
}
