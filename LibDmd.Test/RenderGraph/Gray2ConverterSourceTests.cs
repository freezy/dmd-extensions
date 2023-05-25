using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class Gray2ConverterSourceTests : TestBase
	{
		private RenderGraph _graph;
		private SourceGray2 _source;
		private Func<DmdFrame, ColoredFrame> _convert;

		[SetUp]
		public void Setup()
		{
			_graph = new RenderGraph();
			_source = new SourceGray2();
		}

		[TestCase]
		public async Task Should_Color_Frame()
		{
			var dest = new DestinationFixedGray2Colored(8, 4);
			var palette = new[] { Colors.White, Colors.Tomato, Colors.SpringGreen, Colors.Navy };

			_convert = dmdFrame => new ColoredFrame(dmdFrame, palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000",
				palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Upscale_With_HDScaling()
		{
			var dest = new DestinationFixedGray2Colored(16, 8);
			var palette = new[] { Colors.White, Colors.Tomato, Colors.SpringGreen, Colors.Navy };

			_convert = dmdFrame => new ColoredFrame(dmdFrame, palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				3333333333333333
				3333333333333333
				0022002200220022
				0022002200220022
				1100110011001100
				1100110011001100
				0000000000000000
				0000000000000000",
				palette);

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Downscale_Double()
		{
			var dest = new DestinationFixedGray2Colored(8, 4);
			var palette = new[] { Colors.White, Colors.Tomato, Colors.SpringGreen, Colors.Navy };

			_convert = dmdFrame => new ColoredFrame(dmdFrame.TransformHdScaling(ScalerMode.Doubler), palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000",
				palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}
	}
}
