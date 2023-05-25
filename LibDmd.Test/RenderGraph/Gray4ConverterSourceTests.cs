using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class Gray4ConverterSourceTests : TestBase
	{
		private RenderGraph _graph;
		private SourceGray4 _source;
		private Func<DmdFrame, ColoredFrame> _convert;
		private Color[] _palette;

		[SetUp]
		public void Setup()
		{
			_graph = new RenderGraph();
			_source = new SourceGray4();
			_palette = new[] {
				Colors.AntiqueWhite, Colors.Aqua, Colors.BlueViolet, Colors.BurlyWood,
				Colors.Chartreuse, Colors.Crimson, Colors.DarkGreen, Colors.DeepPink,
				Colors.Firebrick, Colors.Fuchsia, Colors.Goldenrod, Colors.Green,
				Colors.Khaki, Colors.Lime, Colors.MistyRose, Colors.Red,
			};
		}

		[TestCase]
		public async Task Should_Color_Frame()
		{
			var dest = new DestinationFixedGray4Colored(8, 4);
			_convert = dmdFrame => new ColoredFrame(dmdFrame, _palette);

			var frame = FrameGenerator.FromString(@"
				54b2e6b1
				0763f9d7
				31f8004f
				347e13ef");

			var coloredFrame = FrameGenerator.FromString(@"
				54b2e6b1
				0763f9d7
				31f8004f
				347e13ef",
				_palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray4(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Upscale_With_HDScaling()
		{
			var dest = new DestinationFixedGray4Colored(16, 8);
			_convert = dmdFrame => new ColoredFrame(dmdFrame, _palette);

			var frame = FrameGenerator.FromString(@"
				26ada71a
				81ac0bd3
				d76d4cb5
				4f562612");

			var coloredFrame = FrameGenerator.FromString(@"
				2266AADDAA7711AA
				2266AADDAA7711AA
				8811AACC00BBDD33
				8811AACC00BBDD33
				DD7766DD44CCBB55
				DD7766DD44CCBB55
				44FF556622661122
				44FF556622661122",
				_palette);

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Converter = new ConverterGray4(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Downscale_Double()
		{
			var dest = new DestinationFixedGray4Colored(8, 4);
			_convert = dmdFrame => new ColoredFrame(dmdFrame.TransformHdScaling(ScalerMode.Doubler), _palette);

			var frame = FrameGenerator.FromString(@"
				7f0329b7
				cb34c79d
				d8118169
				323c4695");

			var coloredFrame = FrameGenerator.FromString(@"
				7f0329b7
				cb34c79d
				d8118169
				323c4695",
				_palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray4(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Downscale()
		{
			var dest = new DestinationFixedGray4Colored(8, 4);

			_convert = dmdFrame => new ColoredFrame(FrameGenerator.FromString(@"
				fffeeedddcccbbbaaa999888
				aaaaaaaaaaaa000000000000
				000000000000222222222222
				000111222333444555666777"), _palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				fedcba98
				aaaa0000
				00002222
				01234567",
				_palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray4(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Ignore_Colored_Frame_For_Gray4()
		{
			var dest = new DestinationFixedGray4(8, 4);

			_convert = dmdFrame => new ColoredFrame(FrameGenerator.FromString(@"
				00000000
				00000000
				00000000
				00000000"), _palette);

			var frame = FrameGenerator.FromString(@"
				fba12959
				b96166ec
				59eb1d57
				4f9f36a5");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray4(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, frame);
		}

		[TestCase]
		public async Task Should_Passthrough_Gray4_Data()
		{
			var source = new SourceColoredGray4();
			var dest = new DestinationFixedGray4(8, 4);

			_convert = dmdFrame => new ColoredFrame(dmdFrame, _palette);

			var coloredFrame = FrameGenerator.FromString(@"
				6dc0decc
				ac0f8051
				aaee45a7
				8b991924", _palette);

			var frame = FrameGenerator.FromString(@"
				6dc0decc
				ac0f8051
				aaee45a7
				8b991924");

			_graph.Source = source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(source, dest, coloredFrame, frame);
		}


		[TestCase]
		public async Task Should_Convert_Gray4_Data_To_Gray2_Data()
		{
			var source = new SourceColoredGray4();
			var dest = new DestinationFixedGray2(8, 4);

			_convert = dmdFrame => new ColoredFrame(dmdFrame, _palette);

			var coloredFrame = FrameGenerator.FromString(@"
				6dc0decc
				ac0f8051
				aaee45a7
				8b991924", _palette);

			var frame = FrameGenerator.FromString(@"
				13303333
				23032010
				22331121
				22220201");

			_graph.Source = source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(source, dest, coloredFrame, frame);
		}

		[TestCase]
		public async Task Should_Color_Frame_And_Convert_To_Rgb24()
		{
			var dest = new DestinationFixedRgb24(8, 4);

			_convert = dmdFrame => new ColoredFrame(dmdFrame, _palette);

			var frame = FrameGenerator.FromString(@"
				d4d258ce
				f3b28688
				925afb4c
				984199c8");

			var coloredFrame = FrameGenerator.FromString(@"
				00 7F 00 8A DC B2 F0 FF 
				FF DE 00 8A B2 00 B2 B2 
				FF 8A DC DA FF 00 7F F0 
				FF B2 7F 00 FF FF F0 B2 ", @"
				FF FF FF 2B 14 22 E6 E4 
				00 B8 80 2B 22 64 22 22 
				00 2B 14 A5 00 80 FF E6 
				00 22 FF FF 00 00 E6 22", @"
				00 00 00 E2 3C 22 8C E1 
				00 87 00 E2 22 00 22 22 
				FF E2 3C 20 00 00 00 8C 
				FF 22 00 FF FF FF 8C 22");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray4(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Color_Frame_And_Convert_To_Bitmap()
		{
			var dest = new DestinationFixedBitmap(8, 4);

			_convert = dmdFrame => new ColoredFrame(dmdFrame, _palette);

			var frame = FrameGenerator.FromString(@"
				d4d258ce
				f3b28688
				925afb4c
				984199c8");

			var coloredFrame = FrameGenerator.FromString(@"
				00 7F 00 8A DC B2 F0 FF 
				FF DE 00 8A B2 00 B2 B2 
				FF 8A DC DA FF 00 7F F0 
				FF B2 7F 00 FF FF F0 B2 ", @"
				FF FF FF 2B 14 22 E6 E4 
				00 B8 80 2B 22 64 22 22 
				00 2B 14 A5 00 80 FF E6 
				00 22 FF FF 00 00 E6 22", @"
				00 00 00 E2 3C 22 8C E1 
				00 87 00 E2 22 00 22 22 
				FF E2 3C 20 00 00 00 8C 
				FF 22 00 FF FF FF 8C 22");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray4(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}
	}
}
