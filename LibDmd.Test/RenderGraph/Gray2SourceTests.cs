using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class Gray2SourceTests : TestBase
	{
		private Gray2TestSource _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			_graph = new RenderGraph();
			_source = new Gray2TestSource();
		}

		[TestCase]
		public async Task Should_Passthrough_Gray2_Frame()
		{
			var dest = new Gray2FixedTestDestination(8, 4);
			
			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			await AssertFrame(_source, dest, frame, frame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_Gray2_Frame_With_Centering_Destination_NoHdScaling()
		{
			var dest = new Gray2FixedTestDestination(16, 8) { DmdAllowHdScaling = false };
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Scale2x;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				11133111
				10333301
				10333301
				11133111");
			
			var scaledFrame = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				0000111331110000
				0000103333010000
				0000103333010000
				0000111331110000
				0000000000000000
				0000000000000000");
			
			await AssertFrame(_source, dest, frame, scaledFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_Gray2_Frame_With_Centering_Graph_NoHdScaling()
		{
			var dest = new Gray2FixedTestDestination(16, 8) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				11111111
				10033001
				10033001
				11111111");
			
			
			var upscaledFrame1 = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				0000111111110000
				0000100330010000
				0000100330010000
				0000111111110000
				0000000000000000
				0000000000000000");

			await AssertFrame(_source, dest, frame1, upscaledFrame1);
		}
		
		[TestCase]
		public async Task Should_Convert_To_Gray2_Frame_With_Downscale_Fit()
		{
			var dest = new Gray2FixedTestDestination(8, 4);
			
			_graph.Source = _source;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				3333232321321030
				3333333300000000
				0000000033333333
				0301231232323333");

			var frame2 = FrameGenerator.FromString(@"
				33333333
				21032101
				10123210
				02321012
				10123210
				21012321
				12321012
				33333333");
			
			var scaledFrame1 = FrameGenerator.FromString(@"
				00000000
				33331101
				10113333
				00000000");
			
			var scaledFrame2 = FrameGenerator.FromString(@"
				00222200
				00122100
				00112100
				00232200");

			await AssertFrame(_source, dest, frame1, scaledFrame1);
			await AssertFrame(_source, dest, frame2, scaledFrame2);
		}
		
		[TestCase]
		public async Task Should_Convert_To_Gray2_Frame_With_Downscale_Fill()
		{
			var dest = new Gray2FixedTestDestination(8, 4);
			
			_graph.Source = _source;
			_graph.Resize = ResizeMode.Fill;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame1 = FrameGenerator.FromString(@"
				3333232321321030
				3333333300000000
				0000000033333333
				0301231232323333");
			
			var frame2 = FrameGenerator.FromString(@"
				33333333
				21032101
				10123210
				02321012
				10123210
				21012321
				12321012
				33333333");
			
			var scaledFrame1 = FrameGenerator.FromString(@"
				23232132
				33330000
				00003333
				23123232");
			
			var scaledFrame2 = FrameGenerator.FromString(@"
				10123210
				02321012
				10123210
				21012321");
			
			await AssertFrame(_source, dest, frame1, scaledFrame1);
			await AssertFrame(_source, dest, frame2, scaledFrame2);
		}
		
		[TestCase]
		public async Task Should_Convert_To_Gray2_Frame_With_Downscale_Stretch()
		{
			var dest = new Gray2FixedTestDestination(8, 4);
			
			_graph.Source = _source;
			_graph.Resize = ResizeMode.Stretch;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				3333232321321030
				3333333300000000
				0000000033333333
				0301231232323333");
			
			var scaledFrame = FrameGenerator.FromString(@"
				33222202
				33330000
				00003333
				20222233");
			
			await AssertFrame(_source, dest, frame, scaledFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_Gray2_Frame_With_HDScaling_Double()
		{
			var dest = new Gray2FixedTestDestination(16, 8);
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");
			
			var scaledFrame = FrameGenerator.FromString(@"
				3333333333333333
				3333333333333333
				0022002200220022
				0022002200220022
				1100110011001100
				1100110011001100
				0000000000000000
				0000000000000000");
				
			await AssertFrame(_source, dest, frame, scaledFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_Gray2_Frame_With_HDScaling_Scale2X()
		{
			var dest = new Gray2FixedTestDestination(16, 8);
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Scale2x;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				00000000
				00312300
				00332300
				00000000");
			
			var scaledFrame = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				0000031122300000
				0000333122330000
				0000333322330000
				0000033322300000
				0000000000000000
				0000000000000000");
			
			await AssertFrame(_source, dest, frame, scaledFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_RGB24_Frame()
		{
			var dest = new Rgb24TestDestination(8, 4);
			
			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");
			
			var rgbFrame = FrameGenerator.FromString(@"
				FF FF FF FF FF FF FF FF 
				00 AA 00 AA 00 AA 00 AA 
				55 00 55 00 55 00 55 00 
				00 00 00 00 00 00 00 00", @"
				45 45 45 45 45 45 45 45 
				00 2E 00 2E 00 2E 00 2E 
				17 00 17 00 17 00 17 00 
				00 00 00 00 00 00 00 00", @"
				00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00");

			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Frame_With_HDScaling_Double()
		{
			var dest = new Rgb24TestDestination(16, 8) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");
			
			var rgbFrame = FrameGenerator.FromString(@"
				FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF 
				FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF 
				00 00 AA AA 00 00 AA AA 00 00 AA AA 00 00 AA AA 
				00 00 AA AA 00 00 AA AA 00 00 AA AA 00 00 AA AA 
				55 55 00 00 55 55 00 00 55 55 00 00 55 55 00 00 
				55 55 00 00 55 55 00 00 55 55 00 00 55 55 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", @"
				45 45 45 45 45 45 45 45 45 45 45 45 45 45 45 45 
				45 45 45 45 45 45 45 45 45 45 45 45 45 45 45 45 
				00 00 2E 2E 00 00 2E 2E 00 00 2E 2E 00 00 2E 2E 
				00 00 2E 2E 00 00 2E 2E 00 00 2E 2E 00 00 2E 2E 
				17 17 00 00 17 17 00 00 17 17 00 00 17 17 00 00 
				17 17 00 00 17 17 00 00 17 17 00 00 17 17 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", @"
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
			
			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Frame_With_HDScaling_2X()
		{
			var dest = new Rgb24TestDestination(16, 8) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Scale2x;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				00000000
				00333000
				00333000
				00000000");
			
			var rgbFrame = FrameGenerator.FromString(@"
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 FF FF FF FF 00 00 00 00 00 00 00 
				00 00 00 00 FF FF FF FF FF FF 00 00 00 00 00 00 
				00 00 00 00 FF FF FF FF FF FF 00 00 00 00 00 00 
				00 00 00 00 00 FF FF FF FF 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", @"
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 45 45 45 45 00 00 00 00 00 00 00 
				00 00 00 00 45 45 45 45 45 45 00 00 00 00 00 00 
				00 00 00 00 45 45 45 45 45 45 00 00 00 00 00 00 
				00 00 00 00 00 45 45 45 45 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", @"
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
			
			await AssertFrame(_source, dest, frame, rgbFrame);
		}
	}
}
