using System.Collections.Generic;
using System.Threading.Tasks;
using LibDmd.Common;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class Gray2DynamicSourceTests : TestBase
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
			var dest = new Gray2DynamicTestDestination();
			
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
		public async Task Should_Passthrough_Gray2_Frame_Flipped_Horizontally()
		{
			var dest = new Gray2DynamicTestDestination();
			
			_graph.Source = _source;
			_graph.FlipHorizontally = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");
			
			var frameFlipped = FrameGenerator.FromString(@"
				33333333
				20202020
				01010101
				00000000");

			await AssertFrame(_source, dest, frame, frameFlipped);
		}
				
		[TestCase]
		public async Task Should_Passthrough_Gray2_Frame_Flipped_Vertically()
		{
			var dest = new Gray2DynamicTestDestination();
			
			_graph.Source = _source;
			_graph.FlipVertically = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");
			
			var frameFlipped = FrameGenerator.FromString(@"
				00000000
				10101010
				02020202
				33333333");

			await AssertFrame(_source, dest, frame, frameFlipped);
		}
		
		[TestCase]
		public async Task Should_Passthrough_Gray2_Frame_Flipped()
		{
			var dest = new Gray2DynamicTestDestination();
			
			_graph.Source = _source;
			_graph.FlipVertically = true;
			_graph.FlipHorizontally = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");
			
			var frameFlipped = FrameGenerator.FromString(@"
				00000000
				01010101
				20202020
				33333333");

			await AssertFrame(_source, dest, frame, frameFlipped);
		}

		[TestCase]
		public async Task Should_Convert_To_Gray2_Frame_With_HDScaling_Double()
		{
			var dest = new Gray2DynamicTestDestination();
			
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
			var dest = new Gray2DynamicTestDestination();
			
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
			var dest = new Rgb24DynamicTestDestination();
			
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
			var dest = new Rgb24DynamicTestDestination();
			
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
			var dest = new Rgb24DynamicTestDestination();
			
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
