using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;

namespace LibDmd.Test.Stubs
{
	public class ConverterRgb565 : AbstractConverter, IRgb565Source
	{
		public override string Name => "Converter[Gray2 -> RGB565]";
		public override IEnumerable<FrameFormat> From => new[] { FrameFormat.Gray2 };

		private readonly Func<DmdFrame, DmdFrame> _convert;
		private readonly Subject<DmdFrame> _rgb565Frames = new Subject<DmdFrame>();

		public ConverterRgb565(Func<DmdFrame, DmdFrame> convert) : base(true)
			=> _convert = convert;

		public IObservable<DmdFrame> GetRgb565Frames() => _rgb565Frames;

		public override bool Supports(FrameFormat format) => true;

		public override void Convert(DmdFrame frame) => _rgb565Frames.OnNext(_convert(frame));
	}
}
