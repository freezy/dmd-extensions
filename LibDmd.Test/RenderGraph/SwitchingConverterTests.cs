using System.Collections.Generic;
using System.Threading.Tasks;
using LibDmd.Converter;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class SwitchingConverterTests : TestBase
	{
		private SourceGray2 _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			_graph = new RenderGraph(new UndisposedReferences(), true);
			_source = new SourceGray2();
		}

		[TearDown]
		public void Teardown()
		{
			_graph.Dispose();
		}

		[TestCase]
		public async Task Should_Switch_To_Rgb565_Converter()
		{
			var dest = new DestinationFixedRgb565(2, 2);
			var rgb565Frame = new DmdFrame(new Dimensions(2, 2), new byte[] {
				0xe4, 0x00, 0x07, 0x04,
				0xe7, 0xfd, 0x3b, 0xff
			}, 16);
			var converter = new SwitchingConverter();
			converter.Switch(new ConverterRgb565(_ => rgb565Frame));

			_graph.Source = _source;
			_graph.Converter = converter;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var gray2Frame = FrameGenerator.FromString(@"
				01
				23");

			await AssertFrame(_source, dest, gray2Frame, rgb565Frame);
		}
	}
}
