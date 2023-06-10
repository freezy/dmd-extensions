using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class MultiGraphTests : TestBase
	{
		private Func<DmdFrame, ColoredFrame> _convert;

		[SetUp]
		public void Setup()
		{
			AddLogger();
		}

		[TearDown]
		public void Teardown()
		{
			RemoveLogger();
		}

		[TestCase]
		public async Task Should_Output_Only_Once_For_Single_Output_Converter()
		{
			_convert = dmdFrame => FrameGenerator.RandomColored(128, 32, 2);
			var source2 = new SourceGray2();
			var source4 = new SourceGray4();
			var dest = new DestinationFixedGray2Colored(128, 32);
			var renderers = new List<IDestination> { dest };
			var gc = new RenderGraphCollection();
			var converter = new ConverterGray2(_convert);
			gc.Add(new RenderGraph(true) {
				Name = "2-bit Test Graph",
				Source = source2,
				Destinations = renderers,
				Converter = converter
			});
			gc.Add(new RenderGraph(true) {
				Name = "4-bit Test Graph",
				Source = source4,
				Destinations = renderers,
				Converter = converter
			});
			gc.StartRendering();

			dest.Reset();
			source2.AddFrame(FrameGenerator.Random(128, 32, 2));
			await dest.Frame;

			dest.NumFrames.Should().Be(1);

			gc.Dispose();
		}

		[TestCase]
		public async Task Should_Output_Only_Once_For_Multi_Output_Converter()
		{
			_convert = dmdFrame => FrameGenerator.RandomColored(128, 32, 2);
			var source2 = new SourceGray2();
			var source4 = new SourceGray4();
			var dest = new DestinationFixedGray2Colored(128, 32);
			var renderers = new List<IDestination> { dest };
			var gc = new RenderGraphCollection();
			var converter = new ConverterGray2Multi(_convert);
			gc.Add(new RenderGraph(true) {
				Name = "2-bit Test Graph",
				Source = source2,
				Destinations = renderers,
				Converter = converter
			});
			gc.Add(new RenderGraph(true) {
				Name = "4-bit Test Graph",
				Source = source4,
				Destinations = renderers,
				Converter = converter
			});

			gc.StartRendering();

			dest.Reset();
			source2.AddFrame(FrameGenerator.Random(128, 32, 2));
			await dest.Frame;

			dest.NumFrames.Should().Be(1);

			gc.Dispose();
		}

		[TestCase]
		public async Task Should_Output_Only_Once_For_Multi_Output_Converter_With_MultiDest()
		{
			_convert = dmdFrame => FrameGenerator.RandomColored(128, 32, 6);
			var source2 = new SourceGray2();
			var source4 = new SourceGray4();
			var dest1 = new DestinationFixedGray6Colored(128, 32);
			var dest2 = new DestinationFixedGray2Colored(128, 32);
			var renderers = new List<IDestination> { dest1, dest2 };
			var gc = new RenderGraphCollection();
			var converter = new ConverterGray2Multi(_convert);
			gc.Add(new RenderGraph(true) {
				Name = "2-bit Test Graph",
				Source = source2,
				Destinations = renderers,
				Converter = converter
			});
			gc.Add(new RenderGraph(true) {
				Name = "4-bit Test Graph",
				Source = source4,
				Destinations = renderers,
				Converter = converter
			});

			gc.StartRendering();

			dest1.Reset();
			dest2.Reset();
			source2.AddFrame(FrameGenerator.Random(128, 32, 2));

			await dest1.Frame;
			await dest2.Frame;

			dest1.NumFrames.Should().Be(1);
			dest2.NumFrames.Should().Be(1);

			gc.Dispose();
		}
	}
}
