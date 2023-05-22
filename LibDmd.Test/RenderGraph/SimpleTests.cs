using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class SimpleTests : TestBase
	{
	
		[SetUp]
		public void Setup()
		{
		}

		[TestCase]
		public async Task Should_Send_And_Receive_The_Same_Gray2_Frame()
		{
			var source = new TestSource();
			var dest = new TestGray2Destination(8, 4);
			var graph = new RenderGraph();
			graph.Source = source;
			graph.Destinations = new List<IDestination>	{ dest };
			var frame = FrameGenerator.FromString(@"
				00000000
				10101010
				01010101
				11111111");

			Print(frame);

			graph.StartRendering();
			source.AddFrame(frame);
			var destFrame = await dest.LastFrame;
			
			destFrame.Should().BeEquivalentTo(frame.Data);
		}
		
		[TestCase]
		public async Task Should_Send_And_Receive_Gray2_Frame_At_Double_Size()
		{
			var source = new TestSource();
			var dest = new TestGray2Destination(16, 8);
			var graph = new RenderGraph();
			graph.Source = source;
			graph.Destinations = new List<IDestination>	{ dest };
			graph.ScalerMode = ScalerMode.Scale2x;
			var frameIn = FrameGenerator.FromString(@"
				11110000
				11110000
				00001111
				00001111");
			var frameOut = FrameGenerator.FromString(@"
				1111111100000000
				1111111100000000
				1111111100000000
				1111111100000000
				0000000011111111
				0000000011111111
				0000000011111111
				0000000011111111");

			graph.StartRendering();
			source.AddFrame(frameIn);
			var destFrame = await dest.LastFrame;
			
			Print(new DmdFrame(new Dimensions(16, 8), destFrame, 2));
			
			destFrame.Should().BeEquivalentTo(frameOut.Data);
		}
	}
}
