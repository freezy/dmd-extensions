using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class Gray6FixedSourceTests : TestBase
	{
		private SourceGray6 _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			_graph = new RenderGraph();
			_source = new SourceGray6();
		}

		[TestCase]
		public async Task Should_Passthrough_Frame()
		{
			var dest = new DestinationFixedGray6(8, 4);
			
			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var gray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d");

			await AssertFrame(_source, dest, gray6Frame, gray6Frame);
		}

		[TestCase]
		public async Task Should_Convert_To_Gray2()
		{
			var dest = new DestinationFixedGray2(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var gray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d");

			var expectedFrame = FrameGenerator.FromString(@"
				30012133
				03020011
				02001200
				01013223");

			await AssertFrame(_source, dest, gray6Frame, expectedFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_Gray4()
		{
			var dest = new DestinationFixedGray4(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var gray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d");

			var expectedFrame = FrameGenerator.FromString(@"
				F016A5CD
				0E192347
				0B104A13
				0617C89F");

			await AssertFrame(_source, dest, gray6Frame, expectedFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_RGB24()
		{
			var dest = new DestinationFixedRgb24(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.SetColor(Colors.White);
			_graph.StartRendering();

			var gray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d");

			var rgbFrame = FrameGenerator.FromWhiteString(@"
				FF 00 10 6D A2 51 CA D2
				04 E7 14 92 24 3D 4D 71
				08 B6 18 00 41 A2 10 39
				0C 65 1C 75 C6 8E 96 F7");

			await AssertFrame(_source, dest, gray6Frame, rgbFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_Bitmap()
		{
			var dest = new DestinationFixedBitmap(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.SetColor(Colors.White);
			_graph.StartRendering();

			var gray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d");

			var rgbFrame = FrameGenerator.FromWhiteString(@"
				FF 00 10 6D A2 51 CA D2
				04 E7 14 92 24 3D 4D 71
				08 B6 18 00 41 A2 10 39
				0C 65 1C 75 C6 8E 96 F7");

			await AssertFrame(_source, dest, gray6Frame, rgbFrame);
		}
	}
}
