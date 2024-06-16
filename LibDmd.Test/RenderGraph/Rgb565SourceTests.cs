using System.Collections.Generic;
using System.Threading.Tasks;
using LibDmd.Common;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class Rgb565SourceTests : TestBase
	{
		private SourceRgb565 _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			Profiler.Reset();

			_graph = new RenderGraph(new UndisposedReferences(), true);
			_source = new SourceRgb565();
		}

		[TearDown]
		public void Teardown()
		{
			_graph.Dispose();

			AddLogger();
			Profiler.Print();
			RemoveLogger();
		}

		[TestCase]
		public async Task Should_Passthrough_Frame()
		{
			var dest = new DestinationFixedRgb565(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var rgb565Frame = FrameGenerator.FromString(@"
				96b0 fbea 74fc 2880 1956 9185 54ad 5f65
				7449 5433 0aa6 1435 5614 e656 3260 8a1c
				0abd 8030 1892 26a9 e981 6a19 e98d 1a68
				615a e6e6 6c60 897a 9e8d a19c e9c0 a9e8");

			await AssertFrame(_source, dest, rgb565Frame, rgb565Frame);
		}
	}
}
