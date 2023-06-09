using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Input;
using NLog;

namespace LibDmd.Test.Stubs
{
	public class ConverterGray4 : IConverter, IColoredGray4Source
	{
		public string Name => "Converter[Gray4]";
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }
		public IEnumerable<FrameFormat> From => new [] {FrameFormat.Gray4};

		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray4Frames = new Subject<ColoredFrame>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ConverterGray4(Func<DmdFrame, ColoredFrame> convert)
		{
			_convert = convert;
		}

		public IObservable<ColoredFrame> GetColoredGray4Frames() => _coloredGray4Frames;

		public void Convert(DmdFrame frame)
		{
			Logger.Info($"ConverterGray4.Convert");
			_coloredGray4Frames.OnNext(_convert(frame));
		}

		public void Convert(AlphaNumericFrame frame) { }

		public void Init()
		{
		}

		public void SetDimensions(Dimensions dim)
		{
		}
	}
}
