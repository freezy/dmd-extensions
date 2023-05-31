using System.Collections.Generic;
using System.Threading.Tasks;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class BitmapFixedSourceTests : TestBase
	{
		private SourceBitmap _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			_graph = new RenderGraph(true);
			_source = new SourceBitmap();
		}

		[TearDown]
		public void Teardown()
		{
			_graph.Dispose();
		}

		[TestCase]
		public async Task Should_Passthrough_Frame()
		{
			var dest = new DestinationFixedBitmap(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var bitmapFrame = FrameGenerator.FromString(@"
				96 b0 ca fb ea 36 74 fc
				74 49 b0 54 33 08 0a a6
				0a bd 28 80 30 9a 18 92
				95 49 b8 5d 90 0d b9 ea", @"
				23 18 ce f5 04 57 65 c5
				1c a6 e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 4e 9c
				78 a4 43 44 eb c9 28 57", @"
				c9 76 3b 6a 86 4b df 6a
				1f 9f aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 d7 92
				e0 9f 9d dc de 95 15 ea").ConvertToBmp();

			await AssertFrame(_source, dest, bitmapFrame, bitmapFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_Gray2()
		{
			var dest = new DestinationFixedGray2(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var bitmapFrame = FrameGenerator.FromString(@"
				ff b0 ca fb ea 36 74 fc
				74 49 b0 54 33 08 0a a6
				0a bd 28 80 30 9a 18 92
				78 49 b8 5d 90 0d b9 00", @"
				ff 18 ce f5 04 57 65 c5
				1c a6 e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 4e 9c
				34 a4 43 44 eb c9 28 00", @"
				ff 76 3b 6a 86 4b df 6a
				1f 9f aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 d7 92
				e0 9f 9d dc de 95 15 00").ConvertToBmp();

			var expectedFrame = FrameGenerator.FromString(@"
				31221122
				11212111
				12220212
				21122110");

			await AssertFrame(_source, dest, bitmapFrame, expectedFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_Gray4()
		{
			var dest = new DestinationFixedGray4(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var bmpFrame = FrameGenerator.FromString(@"
				ff b0 ca fb ea 36 74 fc
				74 49 b0 54 33 08 0a a6
				0a bd 28 80 30 9a 18 92
				78 49 b8 5d 90 0d b9 00", @"
				ff 18 ce f5 04 57 65 c5
				1c a6 e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 4e 9c
				34 a4 43 44 eb c9 28 00", @"
				ff 76 3b 6a 86 4b df 6a
				1f 9f aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 d7 92
				e0 9f 9d dc de 95 15 00").ConvertToBmp();

			var expectedFrame = FrameGenerator.FromString(@"
				F68A74AB
				47C59477
				4B8A2C79
				8778B660");

			await AssertFrame(_source, dest, bmpFrame, expectedFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_Rgb24()
		{
			var dest = new DestinationFixedRgb24(8, 4);

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var rgbFrame = FrameGenerator.FromString(@"
				ff b0 ca fb ea 36 74 fc
				74 49 b0 54 33 08 0a a6
				0a bd 28 80 30 9a 18 92
				78 49 b8 5d 90 0d b9 00", @"
				ff 18 ce f5 04 57 65 c5
				1c a6 e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 4e 9c
				34 a4 43 44 eb c9 28 00", @"
				ff 76 3b 6a 86 4b df 6a
				1f 9f aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 d7 92
				e0 9f 9d dc de 95 15 00");

			var bmpFrame = rgbFrame.ConvertToBmp();

			await AssertFrame(_source, dest, bmpFrame, rgbFrame);
		}

		[TestCase]
		public async Task Should_Flip_Horizontally()
		{
			var dest = new DestinationFixedRgb24(8, 4);

			_graph.Source = _source;
			_graph.FlipHorizontally = true;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var bmpFrame = FrameGenerator.FromString(@"
				ff b0 ca fb ea 36 74 fc
				74 49 b0 54 33 08 0a a6
				0a bd 28 80 30 9a 18 92
				78 49 b8 5d 90 0d b9 00", @"
				ff 18 ce f5 04 57 65 c5
				1c a6 e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 4e 9c
				34 a4 43 44 eb c9 28 00", @"
				ff 76 3b 6a 86 4b df 6a
				1f 9f aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 d7 92
				e0 9f 9d dc de 95 15 00").ConvertToBmp();

			var flippedFrame = FrameGenerator.FromString(@"
				FC 74 36 EA FB CA B0 FF
				A6 0A 08 33 54 B0 49 74
				92 18 9A 30 80 28 BD 0A
				00 B9 0D 90 5D B8 49 78", @"
				C5 65 57 04 F5 CE 18 FF
				3A F1 88 68 6C E6 A6 1C
				9C 4E F8 20 9D E8 E1 61
				00 28 C9 EB 44 43 A4 34", @"
				6A DF 4B 86 6A 3B 76 FF
				49 31 6C F8 31 AA 9F 1F
				92 D7 C5 1D E3 AE 95 77
				00 15 95 DE DC 9D 9F E0");

			await AssertFrame(_source, dest, bmpFrame, flippedFrame);
		}

		[TestCase]
		public async Task Should_Upscale_With_Centering_Graph_NoHdScaling()
		{
			var dest = new DestinationFixedRgb24(10, 6) { DmdAllowHdScaling = true };

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var bmpFrame = FrameGenerator.FromString(@"
				ff b0 ca fb ea 36 74 fc
				74 49 b0 54 33 08 0a a6
				0a bd 28 80 30 9a 18 92
				78 49 b8 5d 90 0d b9 00", @"
				ff 18 ce f5 04 57 65 c5
				1c a6 e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 4e 9c
				34 a4 43 44 eb c9 28 00", @"
				ff 76 3b 6a 86 4b df 6a
				1f 9f aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 d7 92
				e0 9f 9d dc de 95 15 00").ConvertToBmp();

			var upscaledFrame = FrameGenerator.FromString(@"
				00 00 00 00 00 00 00 00 00 00
				00 FF B0 CA FB EA 36 74 FC 00
				00 74 49 B0 54 33 08 0A A6 00
				00 0A BD 28 80 30 9A 18 92 00
				00 78 49 B8 5D 90 0D B9 00 00
				00 00 00 00 00 00 00 00 00 00", @"
				00 00 00 00 00 00 00 00 00 00
				00 FF 18 CE F5 04 57 65 C5 00
				00 1C A6 E6 6C 68 88 F1 3A 00
				00 61 E1 E8 9D 20 F8 4E 9C 00
				00 34 A4 43 44 EB C9 28 00 00
				00 00 00 00 00 00 00 00 00 00", @"
				00 00 00 00 00 00 00 00 00 00
				00 FF 76 3B 6A 86 4B DF 6A 00
				00 1F 9F AA 31 F8 6C 31 49 00
				00 77 95 AE E3 1D C5 D7 92 00
				00 E0 9F 9D DC DE 95 15 00 00
				00 00 00 00 00 00 00 00 00 00");

			await AssertFrame(_source, dest, bmpFrame, upscaledFrame);
		}

		[TestCase]
		public async Task Should_Upscale_With_Doubler()
		{
			var dest = new DestinationFixedBitmap(16, 8) { DmdAllowHdScaling = true };

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var rgbFrame = FrameGenerator.FromString(@"
				ff b0 ca fb ea 36 74 fc
				74 49 b0 54 33 08 0a a6
				0a bd 28 80 30 9a 18 92
				78 49 b8 5d 90 0d b9 00", @"
				ff 18 ce f5 04 57 65 c5
				1c a6 e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 4e 9c
				34 a4 43 44 eb c9 28 00", @"
				ff 76 3b 6a 86 4b df 6a
				1f 9f aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 d7 92
				e0 9f 9d dc de 95 15 00").ConvertToBmp();;

			var upscaledFrame = FrameGenerator.FromString(@"
				FF FF B0 B0 CA CA FB FB EA EA 36 36 74 74 FC FC 
				FF FF B0 B0 CA CA FB FB EA EA 36 36 74 74 FC FC 
				74 74 49 49 B0 B0 54 54 33 33 08 08 0A 0A A6 A6 
				74 74 49 49 B0 B0 54 54 33 33 08 08 0A 0A A6 A6 
				0A 0A BD BD 28 28 80 80 30 30 9A 9A 18 18 92 92 
				0A 0A BD BD 28 28 80 80 30 30 9A 9A 18 18 92 92 
				78 78 49 49 B8 B8 5D 5D 90 90 0D 0D B9 B9 00 00 
				78 78 49 49 B8 B8 5D 5D 90 90 0D 0D B9 B9 00 00 ", @"
				FF FF 18 18 CE CE F5 F5 04 04 57 57 65 65 C5 C5 
				FF FF 18 18 CE CE F5 F5 04 04 57 57 65 65 C5 C5 
				1C 1C A6 A6 E6 E6 6C 6C 68 68 88 88 F1 F1 3A 3A 
				1C 1C A6 A6 E6 E6 6C 6C 68 68 88 88 F1 F1 3A 3A 
				61 61 E1 E1 E8 E8 9D 9D 20 20 F8 F8 4E 4E 9C 9C 
				61 61 E1 E1 E8 E8 9D 9D 20 20 F8 F8 4E 4E 9C 9C 
				34 34 A4 A4 43 43 44 44 EB EB C9 C9 28 28 00 00 
				34 34 A4 A4 43 43 44 44 EB EB C9 C9 28 28 00 00", @"
				FF FF 76 76 3B 3B 6A 6A 86 86 4B 4B DF DF 6A 6A 
				FF FF 76 76 3B 3B 6A 6A 86 86 4B 4B DF DF 6A 6A 
				1F 1F 9F 9F AA AA 31 31 F8 F8 6C 6C 31 31 49 49 
				1F 1F 9F 9F AA AA 31 31 F8 F8 6C 6C 31 31 49 49 
				77 77 95 95 AE AE E3 E3 1D 1D C5 C5 D7 D7 92 92 
				77 77 95 95 AE AE E3 E3 1D 1D C5 C5 D7 D7 92 92 
				E0 E0 9F 9F 9D 9D DC DC DE DE 95 95 15 15 00 00 
				E0 E0 9F 9F 9D 9D DC DC DE DE 95 95 15 15 00 00").ConvertToBmp();

			await AssertFrame(_source, dest, rgbFrame, upscaledFrame);
		}

		[TestCase]
		public async Task Should_Upscale_With_Scale2x()
		{
			var dest = new DestinationFixedBitmap(16, 8) { DmdAllowHdScaling = true };

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Scale2x;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var rgbFrame = FrameGenerator.FromString(@"
				ff b0 ca fb ea 36 74 fc
				74 49 b0 54 33 08 0a a6
				0a bd 28 80 30 9a 18 92
				78 49 b8 5d 90 0d b9 00", @"
				ff 18 ce f5 04 57 65 c5
				1c a6 e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 4e 9c
				34 a4 43 44 eb c9 28 00", @"
				ff 76 3b 6a 86 4b df 6a
				1f 9f aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 d7 92
				e0 9f 9d dc de 95 15 00").ConvertToBmp();;

			var upscaledFrame = FrameGenerator.FromString(@"
				FF FF B0 B0 CA CA FB FB EA EA 36 36 74 74 FC FC 
				FF FF B0 B0 CA CA FB FB EA EA 36 36 74 74 FC FC 
				74 74 49 49 B0 B0 54 54 33 33 08 08 0A 0A A6 A6 
				74 74 49 49 B0 B0 54 54 33 33 08 08 0A 0A A6 A6 
				0A 0A BD BD 28 28 80 80 30 30 9A 9A 18 18 92 92 
				0A 0A BD BD 28 28 80 80 30 30 9A 9A 18 18 92 92 
				78 78 49 49 B8 B8 5D 5D 90 90 0D 0D B9 B9 00 00 
				78 78 49 49 B8 B8 5D 5D 90 90 0D 0D B9 B9 00 00 ", @"
				FF FF 18 18 CE CE F5 F5 04 04 57 57 65 65 C5 C5 
				FF FF 18 18 CE CE F5 F5 04 04 57 57 65 65 C5 C5 
				1C 1C A6 A6 E6 E6 6C 6C 68 68 88 88 F1 F1 3A 3A 
				1C 1C A6 A6 E6 E6 6C 6C 68 68 88 88 F1 F1 3A 3A 
				61 61 E1 E1 E8 E8 9D 9D 20 20 F8 F8 4E 4E 9C 9C 
				61 61 E1 E1 E8 E8 9D 9D 20 20 F8 F8 4E 4E 9C 9C 
				34 34 A4 A4 43 43 44 44 EB EB C9 C9 28 28 00 00 
				34 34 A4 A4 43 43 44 44 EB EB C9 C9 28 28 00 00", @"
				FF FF 76 76 3B 3B 6A 6A 86 86 4B 4B DF DF 6A 6A 
				FF FF 76 76 3B 3B 6A 6A 86 86 4B 4B DF DF 6A 6A 
				1F 1F 9F 9F AA AA 31 31 F8 F8 6C 6C 31 31 49 49 
				1F 1F 9F 9F AA AA 31 31 F8 F8 6C 6C 31 31 49 49 
				77 77 95 95 AE AE E3 E3 1D 1D C5 C5 D7 D7 92 92 
				77 77 95 95 AE AE E3 E3 1D 1D C5 C5 D7 D7 92 92 
				E0 E0 9F 9F 9D 9D DC DC DE DE 95 95 15 15 00 00 
				E0 E0 9F 9F 9D 9D DC DC DE DE 95 95 15 15 00 00").ConvertToBmp();

			await AssertFrame(_source, dest, rgbFrame, upscaledFrame);
		}


		[TestCase]
		public async Task Should_Downscale_With_Fill()
		{
			var dest = new DestinationFixedRgb24(6, 4) { DmdAllowHdScaling = true };

			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fill;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var bmpFrame = FrameGenerator.FromString(@"
				ff ff ca fb ea 36 74 fc
				ff ff b0 54 33 08 0a a6
				0a bd 28 80 30 9a 00 00
				78 49 b8 5d 90 0d 00 00", @"
				ff ff ce f5 04 57 65 c5
				ff ff e6 6c 68 88 f1 3a
				61 e1 e8 9d 20 f8 00 00
				34 a4 43 44 eb c9 00 00", @"
				ff ff 3b 6a 86 4b df 6a
				ff ff aa 31 f8 6c 31 49
				77 95 ae e3 1d c5 00 00
				e0 9f 9d dc de 95 00 00").ConvertToBmp();

			var downscaledFrame = FrameGenerator.FromString(@"
				FF CA FB EA 36 74 
				FF B0 54 33 08 0A 
				BD 28 80 30 9A 00 
				49 B8 5D 90 0D 00", @"
				FF CE F5 04 57 65 
				FF E6 6C 68 88 F1 
				E1 E8 9D 20 F8 00 
				A4 43 44 EB C9 00", @"
				FF 3B 6A 86 4B DF 
				FF AA 31 F8 6C 31 
				95 AE E3 1D C5 00 
				9F 9D DC DE 95 00");

			await AssertFrame(_source, dest, bmpFrame, downscaledFrame);
		}
	}
}
