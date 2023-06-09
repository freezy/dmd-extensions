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
	public class ConverterGray2 : AbstractConverter, IColoredGray2Source
	{
		public string Name => "Converter[Gray2]";
		public IObservable<Unit> OnResume => null;
		public IObservable<Unit> OnPause => null;
		public override IEnumerable<FrameFormat> From => new[] { FrameFormat.Gray2 };
		public override bool IsConnected => _isConnected;

		private bool _isConnected;
		private readonly Func<DmdFrame, ColoredFrame> _convert;

		private readonly Subject<ColoredFrame> _coloredGray2Frames = new Subject<ColoredFrame>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ConverterGray2(Func<DmdFrame, ColoredFrame> convert)
		{
			_convert = convert;
		}

		public IObservable<ColoredFrame> GetColoredGray2Frames()
		{
			_isConnected = true;
			return _coloredGray2Frames;
		}

		public override void Convert(DmdFrame frame)
		{
			Logger.Info($"ConverterGray2.Convert");
			_coloredGray2Frames.OnNext(_convert(frame));
		}

		public override void Convert(AlphaNumericFrame frame) { }

		public override void Init()
		{
		}

		public void SetDimensions(Dimensions dim)
		{
		}
	}
}
