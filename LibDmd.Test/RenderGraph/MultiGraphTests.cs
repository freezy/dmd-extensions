using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
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
		public async Task Should_Color_Frame_And_Output_Only_Once()
		{
			var palette = new[] { Colors.White, Colors.Tomato, Colors.SpringGreen, Colors.Navy };
			_convert = dmdFrame => new ColoredFrame(dmdFrame, palette);

			var source2 = new SourceGray2();
			var source4 = new SourceGray4();
			var dest = new DestinationFixedGray2Colored(8, 4);
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

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			dest.Reset();
			source2.AddFrame(frame);
			await dest.Frame;

			dest.NumFrames.Should().Be(1);

			gc.Dispose();
		}
	}
}
