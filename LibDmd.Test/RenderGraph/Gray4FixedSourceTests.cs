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
	public class Gray4FixedSourceTests : TestBase
	{
		private SourceGray4 _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			_graph = new RenderGraph();
			_source = new SourceGray4();
		}

		[TestCase]
		public async Task Should_Passthrough_Frame()
		{
			var dest = new DestinationFixedGray4(8, 4);
			
			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				FEDCBA98
				7777FFFF
				00007777
				01234567");

			await AssertFrame(_source, dest, frame, frame);
		}


		[TestCase]
		public async Task Should_Convert_To_Gray2()
		{
			var dest = new DestinationFixedGray2(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				FEDCBA98
				7777FFFF
				00007777
				01234567");

			var expectedFrame = FrameGenerator.FromString(@"
				33332222
				11113333
				00001111
				00001111");

			await AssertFrame(_source, dest, frame, expectedFrame);
		}

		[TestCase]
		public async Task Should_Flip_Horizontally()
		{
			var dest = new DestinationFixedGray4(8, 4);

			_graph.Source = _source;
			_graph.FlipHorizontally = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				FEDCBA98
				7777FFFF
				00007777
				01234567");

			var frameFlipped = FrameGenerator.FromString(@"
				89ABCDEF
				FFFF7777
				77770000
				76543210");

			await AssertFrame(_source, dest, frame, frameFlipped);
		}

		[TestCase]
		public async Task Should_Flip_Vertically()
		{
			var dest = new DestinationFixedGray4(8, 4);

			_graph.Source = _source;
			_graph.FlipVertically = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				FEDCBA98
				7777FFFF
				00007777
				01234567");

			var frameFlipped = FrameGenerator.FromString(@"
				01234567
				00007777
				7777FFFF
				FEDCBA98");

			await AssertFrame(_source, dest, frame, frameFlipped);
		}

		[TestCase]
		public async Task Should_Flip_Both()
		{
			var dest = new DestinationFixedGray4(8, 4);

			_graph.Source = _source;
			_graph.FlipVertically = true;
			_graph.FlipHorizontally = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				FEDCBA98
				7777FFFF
				00007777
				01234567");

			var frameFlipped = FrameGenerator.FromString(@"
				76543210
				77770000
				FFFF7777
				89ABCDEF");

			await AssertFrame(_source, dest, frame, frameFlipped);
		}

		[TestCase]
		public async Task Should_Upscale_With_Centering_Destination_NoHdScaling()
		{
			var dest = new DestinationFixedGray4(16, 8) { DmdAllowHdScaling = false };

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Scale2x;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				FEDCCDEF
				70EEEE07
				70FFFF07
				FDC99CDF");

			var scaledFrame = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				0000FEDCCDEF0000
				000070EEEE070000
				000070FFFF070000
				0000FDC99CDF0000
				0000000000000000
				0000000000000000");

			await AssertFrame(_source, dest, frame, scaledFrame);
		}

		[TestCase]
		public async Task Should_Upscale_With_Centering_Graph_NoHdScaling()
		{
			var dest = new DestinationFixedGray4(16, 8) { DmdAllowHdScaling = true };

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				FEDCCDEF
				70EEEE07
				70FFFF07
				FDC99CDF");


			var upscaledFrame1 = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				0000FEDCCDEF0000
				000070EEEE070000
				000070FFFF070000
				0000FDC99CDF0000
				0000000000000000
				0000000000000000");

			await AssertFrame(_source, dest, frame1, upscaledFrame1);
		}

		[TestCase]
		public async Task Should_Upscale_With_Centering_Dest_Too_Small()
		{
			var dest = new DestinationFixedGray4(14, 6) { DmdAllowHdScaling = true };

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				FEDCCDEF
				70EEEE07
				70FFFF07
				FDC99CDF");

			var upscaledFrame1 = FrameGenerator.FromString(@"
				00000000000000
				000FEDCCDEF000
				00070EEEE07000
				00070FFFF07000
				000FDC99CDF000
				00000000000000");

			await AssertFrame(_source, dest, frame1, upscaledFrame1);
		}

		[TestCase]
		public async Task Should_Upscale_With_Centering_Same_Width()
		{
			var dest = new DestinationFixedGray4(16, 8) { DmdAllowHdScaling = true };

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				FFFFFFFFFFFFFFFF
				0123456789ABCDEF
				FEDCBA9876543210
				FFFFFFFFFFFFFFFF");

			var upscaledFrame1 = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				FFFFFFFFFFFFFFFF
				0123456789ABCDEF
				FEDCBA9876543210
				FFFFFFFFFFFFFFFF
				0000000000000000
				0000000000000000");

			await AssertFrame(_source, dest, frame1, upscaledFrame1);
		}

		[TestCase]
		public async Task Should_Upscale_With_HDScaling_Double()
		{
			var dest = new DestinationFixedGray4(16, 8);

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				FEDCBA98
				04030201
				10203040
				00000000");

			var scaledFrame = FrameGenerator.FromString(@"
				FFEEDDCCBBAA9988
				FFEEDDCCBBAA9988
				0044003300220011
				0044003300220011
				1100220033004400
				1100220033004400
				0000000000000000
				0000000000000000");

			await AssertFrame(_source, dest, frame, scaledFrame);
		}

		[TestCase]
		public async Task Should_Upscale_With_HDScaling_Double_With_Padding()
		{
			var dest = new DestinationFixedGray4(18, 10);

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				FEDCBA98
				04030201
				10203040
				00000000");

			var scaledFrame = FrameGenerator.FromString(@"
				000000000000000000
				0FFEEDDCCBBAA99880
				0FFEEDDCCBBAA99880
				000440033002200110
				000440033002200110
				011002200330044000
				011002200330044000
				000000000000000000
				000000000000000000
				000000000000000000");

			await AssertFrame(_source, dest, frame, scaledFrame);
		}

	}
}
