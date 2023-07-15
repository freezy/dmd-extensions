using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class MultiDestinationTests : TestBase
	{
		private SourceGray2 _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			AddLogger();
			_graph = new RenderGraph(true);
			_source = new SourceGray2();
		}

		[TearDown]
		public void Teardown()
		{
			RemoveLogger();
		}

		[TestCase]
		public async Task Should_Output_To_Multiple_Destinations()
		{
			var dest1 = new DestinationFixedGray2(128, 32);
			var dest2 = new DestinationFixedColored(128, 32, false);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest1, dest2 };
			_graph.StartRendering();

			var srcFrame = FrameGenerator.Random(128, 32, 2);

			//Print(srcFrame, "In: ");

			dest1.Reset();
			dest2.Reset();
			_source.AddFrame(srcFrame);
			var receivedFrame1 = await dest1.Frame;
			var receivedFrame2 = await dest2.Frame;

			//Print(receivedFrame1, "Out1: ");
			//Print(receivedFrame2, "Out2: ");

			receivedFrame1.Data.Should().BeEquivalentTo(srcFrame.Data);
			receivedFrame1.BitLength.Should().Be(srcFrame.BitLength);
			receivedFrame1.Dimensions.Should().Be(srcFrame.Dimensions);

			receivedFrame2.Data.Should().BeEquivalentTo(srcFrame.Data);
			receivedFrame2.BitLength.Should().Be(srcFrame.BitLength);
			receivedFrame2.Dimensions.Should().Be(srcFrame.Dimensions);
		}
	}
}
