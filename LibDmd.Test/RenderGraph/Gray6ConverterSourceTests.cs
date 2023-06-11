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
	public class Gray6ConverterSourceTests : TestBase
	{
		private RenderGraph _graph;
		private SourceGray4 _source;
		private Func<DmdFrame, ColoredFrame> _convert;
		private Color[] _palette;

		[SetUp]
		public void Setup()
		{
			Profiler.Reset();

			_graph = new RenderGraph(true);
			_source = new SourceGray4();
			_palette = new[] {
				Colors.AntiqueWhite, Colors.Aqua, Colors.BlueViolet, Colors.BurlyWood,
				Colors.Chartreuse, Colors.Crimson, Colors.DarkGreen, Colors.DeepPink,
				Colors.Firebrick, Colors.Fuchsia, Colors.Indigo, Colors.Green,
				Colors.Khaki, Colors.Lime, Colors.MistyRose, Colors.Red,
				Colors.DarkOrchid, Colors.DarkMagenta, Colors.SlateBlue, Colors.NavajoWhite,
				Colors.Cyan, Colors.WhiteSmoke, Colors.Black, Colors.MintCream,
				Colors.SaddleBrown, Colors.Goldenrod, Colors.Azure, Colors.Plum,
				Colors.MediumSpringGreen, Colors.LightPink, Colors.FloralWhite, Colors.Gainsboro,
				Colors.DarkSalmon, Colors.CornflowerBlue, Colors.MediumTurquoise, Colors.OrangeRed,
				Colors.LightCoral, Colors.SpringGreen, Colors.CadetBlue, Colors.MediumBlue,
				Colors.SandyBrown, Colors.OliveDrab, Colors.LightGoldenrodYellow, Colors.PeachPuff,
				Colors.IndianRed, Colors.SeaGreen, Colors.MediumOrchid, Colors.PaleGreen,
				Colors.YellowGreen, Colors.DarkSeaGreen, Colors.Lavender, Colors.SteelBlue,
				Colors.Sienna, Colors.Violet, Colors.SkyBlue, Colors.MediumSlateBlue,
				Colors.SlateGray, Colors.Wheat, Colors.DarkGray, Colors.GreenYellow,
				Colors.Magenta, Colors.Silver, Colors.Turquoise, Colors.Thistle,
			};
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
		public async Task Should_Color_Frame()
		{
			var dest = new DestinationFixedGray6Colored(8, 4);
			var gray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d");

			Print(gray6Frame, "Test: ");

			_convert = _ => new ColoredFrame(gray6Frame, _palette);

			var gray4Frame = FrameGenerator.FromString(@"
				54b2e6b1
				0763f9d7
				31f8004f
				347e13ef");

			var coloredGray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d",
				_palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray6(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, gray4Frame, coloredGray6Frame);
		}

		[TestCase]
		public async Task Should_Upscale_With_HDScaling()
		{
			var dest = new DestinationFixedGray6Colored(16, 8);
			var gray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d");
			_convert = _ => new ColoredFrame(gray6Frame, _palette);

			var frame = FrameGenerator.FromString(@"
				26ada71a
				81ac0bd3
				d76d4cb5
				4f562612");

			var coloredFrame = FrameGenerator.FromString(@"
				3F 3F 00 00 04 04 1B 1B 28 28 14 14 32 32 34 34 
				3F 3F 00 00 04 04 1B 1B 28 28 14 14 32 32 34 34 
				01 01 39 39 05 05 24 24 09 09 0F 0F 13 13 1C 1C 
				01 01 39 39 05 05 24 24 09 09 0F 0F 13 13 1C 1C 
				02 02 2D 2D 06 06 00 00 10 10 28 28 04 04 0E 0E 
				02 02 2D 2D 06 06 00 00 10 10 28 28 04 04 0E 0E 
				03 03 19 19 07 07 1D 1D 31 31 23 23 25 25 3D 3D 
				03 03 19 19 07 07 1D 1D 31 31 23 23 25 25 3D 3D",
				_palette);

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Converter = new ConverterGray6(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Downscale_Double()
		{
			var dest = new DestinationFixedGray6Colored(8, 4);
			var gray6Frame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d");
			_convert = _ => new ColoredFrame(gray6Frame.TransformHdScaling(ScalerMode.Doubler), _palette);

			var frame = FrameGenerator.FromString(@"
				7f0329b7
				cb34c79d
				d8118169
				323c4695");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray6(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, new ColoredFrame(gray6Frame, _palette));
		}

		[TestCase]
		public async Task Should_Downscale()
		{
			var dest = new DestinationFixedGray6Colored(8, 4);

			_convert = dmdFrame => new ColoredFrame(FrameGenerator.FromString(@"
				3f 3f 3f 2e 2e 2e 1d 1d 1d 0c 0c 0c 1b 1b 1b 2a 2a 2a 39 39 39 48 48 48
				0a 0a 0a 0a 0a 0a 0a 0a 0a 0a 0a 0a 30 30 30 30 30 30 30 30 30 30 30 30
				00 00 00 00 00 00 00 00 00 00 00 00 22 22 22 22 22 22 22 22 22 22 22 22
				00 00 00 11 11 11 22 22 22 33 33 33 04 04 04 15 15 15 26 26 26 37 37 37"), _palette);

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				3F 2E 1D 0C 1B 2A 39 28 
				0A 0A 0A 0A 30 30 30 30 
				00 00 00 00 22 22 22 22 
				00 11 22 33 04 15 26 37",
				_palette);

			_graph.Source = _source;
			_graph.Converter = new ConverterGray6(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Ignore_Colored_Frame_For_Gray4()
		{
			var dest = new DestinationFixedGray4(8, 4);

			_convert = _ => new ColoredFrame(FrameGenerator.FromString(@"
				00 00 00 00 00 00 00 00
				00 00 00 00 00 00 00 00
				00 00 00 00 00 00 00 00
				00 00 00 00 00 00 00 00"), _palette);

			var frame = FrameGenerator.FromString(@"
				3F 2E 1D 0C 1B 2A 39 08 
				0A 0A 0A 0A 30 30 30 30 
				00 00 00 00 22 22 22 22 
				00 11 22 33 04 15 26 37");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray6(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, frame);
		}

		[TestCase]
		public async Task Should_Convert_Gray6_Data_To_Gray2_Data()
		{
			var source = new SourceColoredGray6();
			var dest = new DestinationFixedGray2(8, 4);

			_convert = dmdFrame => new ColoredFrame(dmdFrame, _palette);

			var coloredFrame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d", _palette);

			var frame = FrameGenerator.FromString(@"
				30012133
				03020011
				02001200
				01013223");

			_graph.Source = source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(source, dest, coloredFrame, frame);
		}

		[TestCase]
		public async Task Should_Convert_Gray6_Data_To_Gray4_Data()
		{
			var source = new SourceColoredGray6();
			var dest = new DestinationFixedGray4(8, 4);

			_convert = dmdFrame => new ColoredFrame(dmdFrame, _palette);

			var coloredFrame = FrameGenerator.FromString(@"
				3f 00 04 1b 28 14 32 34
				01 39 05 24 09 0f 13 1c
				02 2d 06 00 10 28 04 0e
				03 19 07 1d 31 23 25 3d", _palette);

			var frame = FrameGenerator.FromString(@"
				F016A5CD
				0E192347
				0B104A13
				0617C89F");

			_graph.Source = source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(source, dest, coloredFrame, frame);
		}

		[TestCase]
		public async Task Should_Color_Frame_And_Convert_To_Rgb24()
		{
			var dest = new DestinationFixedRgb24(8, 4);

			_convert = dmdFrame =>FrameGenerator.FromString(@"
				3F 3F 00 00 04 04 1B 1B 28 28 14 14 32 32 34 34
				3F 3F 00 00 04 04 1B 1B 28 28 14 14 32 32 34 34
				01 01 39 39 05 05 24 24 09 09 0F 0F 13 13 1C 1C
				01 01 39 39 05 05 24 24 09 09 0F 0F 13 13 1C 1C
				02 02 2D 2D 06 06 00 00 10 10 28 28 04 04 0E 0E
				02 02 2D 2D 06 06 00 00 10 10 28 28 04 04 0E 0E
				03 03 19 19 07 07 1D 1D 31 31 23 23 25 25 3D 3D
				03 03 19 19 07 07 1D 1D 31 31 23 23 25 25 3D 3D",
				_palette);

			var frame = FrameGenerator.FromString(@"
				d4d258ce
				f3b28688
				925afb4c
				984199c8");

			var coloredFrame = FrameGenerator.FromString(@"
				D8 FA 7F DD F4 00 E6 A0 
				00 F5 DC F0 FF FF FF 00 
				8A 2E 00 FA 99 F4 7F FF 
				DE DA FF FF 8F FF 00 C0", @"
				BF EB FF A0 A4 FF E6 52 
				FF DE 14 80 00 00 DE FA 
				2B 8B 64 EB 32 A4 FF E4 
				B8 A5 14 B6 BC 45 FF C0", @"
				D8 D7 00 DD 60 FF FA 2D 
				FF B3 3C 80 FF 00 AD 9A 
				E2 57 00 D7 CC 60 00 E1 
				87 20 93 C1 8F 00 7F C0");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray6(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}

		[TestCase]
		public async Task Should_Color_Frame_And_Convert_To_Bitmap()
		{
			var dest = new DestinationFixedBitmap(8, 4);

			_convert = dmdFrame => FrameGenerator.FromString(@"
				3F 2E 1D 0C 1B 2A 39 08 
				0A 0A 0A 0A 30 30 30 30 
				00 00 00 00 22 22 22 22 
				00 11 22 33 04 15 26 37",
				_palette);

			var frame = FrameGenerator.FromString(@"
				00000000
				00000000
				00000000
				00000000");

			var coloredFrame = FrameGenerator.FromString(@"
				D8 BA FF F0 DD FA F5 B2 
				4B 4B 4B 4B 9A 9A 9A 9A 
				FA FA FA FA 48 48 48 48 
				FA 8B 48 46 7F F5 5F 7B", @"
				BF 55 B6 E6 A0 FA DE 22 
				00 00 00 00 CD CD CD CD 
				EB EB EB EB D1 D1 D1 D1 
				EB 00 D1 82 FF F5 9E 68", @"
				D8 D3 C1 8C DD D2 B3 22 
				82 82 82 82 32 32 32 32 
				D7 D7 D7 D7 CC CC CC CC 
				D7 8B CC B4 00 F5 A0 EE");

			_graph.Source = _source;
			_graph.Converter = new ConverterGray6(_convert);
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			await AssertFrame(_source, dest, frame, coloredFrame);
		}
	}
}
