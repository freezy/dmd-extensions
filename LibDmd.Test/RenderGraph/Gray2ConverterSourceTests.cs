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

		[TestCase]
		public async Task Should_Downscale()
		{
			var dest = new DestinationFixedGray2Colored(8, 4);
			var palette = new[] { Colors.White, Colors.Tomato, Colors.SpringGreen, Colors.Navy };

			_convert = dmdFrame => new ColoredFrame(FrameGenerator.FromString(@"
				333333333333333333333333
				222222222222222222222222
				111111111111111111111111
				000000000000000000000000"), palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				33333333
				22222222
				11111111
				00000000",
				palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Downscale_And_Flip_Both()
		{
			var dest = new DestinationFixedGray2Colored(8, 4);
			var palette = new[] { Colors.White, Colors.Tomato, Colors.SpringGreen, Colors.Navy };

			_convert = dmdFrame => new ColoredFrame(FrameGenerator.FromString(@"
				000000000000333333333333
				222222222222111111111111
				333333333333111111111111
				111111111111000000000000"), palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				00001111
				11113333
				11112222
				33330000",
				palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.FlipHorizontally = true;
			_graph.FlipVertically = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Flip_Vertically()
		{
			var dest = new DestinationFixedGray2Colored(8, 4);
			var palette = new[] { Colors.White, Colors.Tomato, Colors.SpringGreen, Colors.Navy };

			_convert = dmdFrame => new ColoredFrame(dmdFrame, palette);

			var frame = FrameGenerator.FromString(@"
				00000000
				01010101
				20202020
				33333333");

			var coloredFrame = FrameGenerator.FromString(@"
				33333333
				20202020
				01010101
				00000000",
				palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.FlipVertically = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Flip_Horizontally()
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
				20202020
				01010101
				00000000",
				palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.FlipHorizontally = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Ignore_Colored_Frame_For_Gray2()
		{
			var dest = new DestinationFixedGray2(8, 4);
			var palette = new[] { Colors.White, Colors.Tomato, Colors.SpringGreen, Colors.Navy };

			_convert = dmdFrame => new ColoredFrame(FrameGenerator.FromString(@"
				00000000
				00000000
				00000000
				00000000"), palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				33333333
				20202020
				01010101
				00000000");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.FlipHorizontally = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Passthrough_Gray2_Data()
		{
			var source = new SourceColoredGray2();
			var dest = new DestinationFixedGray2(8, 4);
			var palette = new[] { Colors.Gray, Colors.Red, Color.FromRgb(0x0, 0xff, 0x0), Colors.Blue };

			_convert = dmdFrame => new ColoredFrame(dmdFrame, palette);

			var coloredFrame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000", palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			_graph.Source = source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(source, dest, coloredFrame, frame);
		}

		[TestCase]
		public async Task Should_Color_Frame_And_Convert_To_Rgb24()
		{
			var dest = new DestinationFixedRgb24(8, 4);
			var palette = new[] { Colors.Gray, Colors.Red, Color.FromRgb(0x0, 0xff, 0x0), Colors.Blue };

			_convert = dmdFrame => new ColoredFrame(dmdFrame, palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				00 00 00 00 00 00 00 00 
				80 00 80 00 80 00 80 00 
				FF 80 FF 80 FF 80 FF 80 
				80 80 80 80 80 80 80 80", @"
				00 00 00 00 00 00 00 00 
				80 FF 80 FF 80 FF 80 FF 
				00 80 00 80 00 80 00 80 
				80 80 80 80 80 80 80 80", @"
				FF FF FF FF FF FF FF FF 
				80 00 80 00 80 00 80 00 
				00 80 00 80 00 80 00 80 
				80 80 80 80 80 80 80 80");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Color_Frame_And_Convert_To_Bitmap()
		{
			var dest = new DestinationFixedBitmap(8, 4);
			var palette = new[] { Colors.Gray, Colors.Red, Color.FromRgb(0x0, 0xff, 0x0), Colors.Blue };

			_convert = dmdFrame => new ColoredFrame(dmdFrame, palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				00 00 00 00 00 00 00 00 
				80 00 80 00 80 00 80 00 
				FF 80 FF 80 FF 80 FF 80 
				80 80 80 80 80 80 80 80", @"
				00 00 00 00 00 00 00 00 
				80 FF 80 FF 80 FF 80 FF 
				00 80 00 80 00 80 00 80 
				80 80 80 80 80 80 80 80", @"
				FF FF FF FF FF FF FF FF 
				80 00 80 00 80 00 80 00 
				00 80 00 80 00 80 00 80 
				80 80 80 80 80 80 80 80");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray2(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

	}
}
