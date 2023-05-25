using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class Gray2FixedSourceTests : TestBase
	{
		private SourceGray2 _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			_graph = new RenderGraph();
			_source = new SourceGray2();
		}

		[TestCase]
		public async Task Should_Passthrough_Frame()
		{
			var dest = new DestinationFixedGray2(8, 4);
			
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
		public async Task Should_Flip_Horizontally()
		{
			var dest = new DestinationFixedGray2(8, 4);
			
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
		public async Task Should_Flip_Vertically()
		{
			var dest = new DestinationFixedGray2(8, 4);
			
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
		public async Task Should_Flip_Both()
		{
			var dest = new DestinationFixedGray2(8, 4);
			
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
		public async Task Should_Upscale_With_Centering_Destination_NoHdScaling()
		{
			var dest = new DestinationFixedGray2(16, 8) { DmdAllowHdScaling = false };
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Scale2x;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				11133111
				10333301
				10333301
				11133111");
			
			var scaledFrame = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				0000111331110000
				0000103333010000
				0000103333010000
				0000111331110000
				0000000000000000
				0000000000000000");
			
			await AssertFrame(_source, dest, frame, scaledFrame);
		}
		
		[TestCase]
		public async Task Should_Upscale_With_Centering_Graph_NoHdScaling()
		{
			var dest = new DestinationFixedGray2(16, 8) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				11111111
				10033001
				10033001
				11111111");
			
			
			var upscaledFrame1 = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				0000111111110000
				0000100330010000
				0000100330010000
				0000111111110000
				0000000000000000
				0000000000000000");

			await AssertFrame(_source, dest, frame1, upscaledFrame1);
		}
		
		[TestCase]
		public async Task Should_Upscale_With_Centering_Dest_Too_Small()
		{
			var dest = new DestinationFixedGray2(14, 6) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				11111111
				10033001
				10033001
				11111111");
			
			var upscaledFrame1 = FrameGenerator.FromString(@"
				00000000000000
				00011111111000
				00010033001000
				00010033001000
				00011111111000
				00000000000000");

			await AssertFrame(_source, dest, frame1, upscaledFrame1);
		}
			
		[TestCase]
		public async Task Should_Upscale_With_Centering_Same_Width()
		{
			var dest = new DestinationFixedGray2(16, 8) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				1111111111111111
				0321100330013210
				3210100330010123
				1111111111111111");
			
			var upscaledFrame1 = FrameGenerator.FromString(@"
				0000000000000000
				0000000000000000
				1111111111111111
				0321100330013210
				3210100330010123
				1111111111111111
				0000000000000000
				0000000000000000");

			await AssertFrame(_source, dest, frame1, upscaledFrame1);
		}

		[TestCase]
		public async Task Should_Upscale_With_HDScaling_Double()
		{
			var dest = new DestinationFixedGray2(16, 8);
			
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
		public async Task Should_Upscale_With_HDScaling_Double_With_Padding()
		{
			var dest = new DestinationFixedGray2(18, 10);
			
			_graph.Source = _source;
			_graph.ScalerMode = ScalerMode.Doubler;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00111100");
			
			var scaledFrame = FrameGenerator.FromString(@"
				000000000000000000
				033333333333333330
				033333333333333330
				000220022002200220
				000220022002200220
				011001100110011000
				011001100110011000
				000001111111100000
				000001111111100000
				000000000000000000");
				
			await AssertFrame(_source, dest, frame, scaledFrame);
		}
		
		[TestCase]
		public async Task Should_Upscale_With_HDScaling_Scale2X()
		{
			var dest = new DestinationFixedGray2(16, 8);
			
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
		public async Task Should_Downscale_Fit()
		{
			var dest = new DestinationFixedGray2(8, 4);

			_graph.Source = _source;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				3333232321321030
				3333333300000000
				0000000033333333
				0301231232323333");

			var frame2 = FrameGenerator.FromString(@"
				33333333
				21032101
				10123210
				02321012
				10123210
				21012321
				12321012
				33333333");

			var scaledFrame1 = FrameGenerator.FromString(@"
				00000000
				33331101
				10113333
				00000000");

			var scaledFrame2 = FrameGenerator.FromString(@"
				00222200
				00122100
				00112100
				00232200");

			await AssertFrame(_source, dest, frame1, scaledFrame1);
			await AssertFrame(_source, dest, frame2, scaledFrame2);
		}

		[TestCase]
		public async Task Should_Downscale_Fill()
		{
			var dest = new DestinationFixedGray2(8, 4);

			_graph.Source = _source;
			_graph.Resize = ResizeMode.Fill;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame1 = FrameGenerator.FromString(@"
				3333232321321030
				3333333300000000
				0000000033333333
				0301231232323333");

			var frame2 = FrameGenerator.FromString(@"
				33333333
				21032101
				10123210
				02321012
				10123210
				21012321
				12321012
				33333333");

			var scaledFrame1 = FrameGenerator.FromString(@"
				23232132
				33330000
				00003333
				23123232");

			var scaledFrame2 = FrameGenerator.FromString(@"
				10123210
				02321012
				10123210
				21012321");

			await AssertFrame(_source, dest, frame1, scaledFrame1);
			await AssertFrame(_source, dest, frame2, scaledFrame2);
		}

		[TestCase]
		public async Task Should_Downscale_Stretch()
		{
			var dest = new DestinationFixedGray2(8, 4);

			_graph.Source = _source;
			_graph.Resize = ResizeMode.Stretch;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				3333232321321030
				3333333300000000
				0000000033333333
				0301231232323333");

			var scaledFrame = FrameGenerator.FromString(@"
				33222202
				33330000
				00003333
				20222233");

			await AssertFrame(_source, dest, frame, scaledFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_RGB24()
		{
			var dest = new DestinationFixedRgb24(8, 4);
			
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
		public async Task Should_Convert_To_RGB24_Flipped_Both()
		{
			var dest = new DestinationFixedRgb24(8, 4);
			
			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.FlipHorizontally = true;
			_graph.FlipVertically = true;
			_graph.SetColor(Colors.White);
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 00 00 00 00 00 
				00 55 00 55 00 55 00 55 
				AA 00 AA 00 AA 00 AA 00 
				FF FF FF FF FF FF FF FF");

			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_With_HDScaling_Double()
		{
			var dest = new DestinationFixedRgb24(16, 8) { DmdAllowHdScaling = true };
			
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
		public async Task Should_Convert_To_RGB24_Upscaled_With_HDScaling_2X()
		{
			var dest = new DestinationFixedRgb24(16, 8) { DmdAllowHdScaling = true };
			
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
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Downscaled_Fit()
		{
			var dest = new DestinationFixedRgb24(10, 4) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame1 = FrameGenerator.FromString(@"
				3333333333
				3000000003
				3000000003
				3001111003
				3001111003
				3001111003
				3001111003
				3000000003
				3000000003
				3333333333");
			
			var frame2 = FrameGenerator.FromString(@"
				30000003333330000003
				30000001111110000003
				30000001111110000003
				30000003333330000003");

			const string rgb1 = @"
				00 00 00 A3 66 66 A3 00 00 00 
				00 00 00 66 45 3D 66 00 00 00 
				00 00 00 66 3D 36 66 00 00 00 
				00 00 00 A3 66 66 A3 00 00 00 ";
			
			const string rgb2 = @"
				00 00 00 00 00 00 00 00 00 00 
				80 00 00 55 AA AA 55 00 00 80 
				80 00 00 55 AA AA 55 00 00 80 
				00 00 00 00 00 00 00 00 00 00";
							
			var rgbFrame1 = FrameGenerator.FromString(rgb1, rgb1, rgb1);
			var rgbFrame2 = FrameGenerator.FromString(rgb2, rgb2, rgb2);
			
			await AssertFrame(_source, dest, frame1, rgbFrame1);
			await AssertFrame(_source, dest, frame2, rgbFrame2);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Downscaled_Fill()
		{
			var dest = new DestinationFixedRgb24(10, 4) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.Resize = ResizeMode.Fill;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame1 = FrameGenerator.FromString(@"
				3333333333
				3000000003
				3000000003
				3001111003
				3001111003
				3001111003
				3001111003
				3000000003
				3000000003
				3333333333");
			
			var frame2 = FrameGenerator.FromString(@"
				3333333333333333
				3000011111100003
				3000011111100003
				3333333333333333");

			const string rgb1 = @"
				FF 00 00 55 55 55 55 00 00 FF 
				FF 00 00 55 55 55 55 00 00 FF 
				FF 00 00 55 55 55 55 00 00 FF 
				FF 00 00 55 55 55 55 00 00 FF";
			
			const string rgb2 = @"
				FF FF FF FF FF FF FF FF FF FF 
				00 00 55 55 55 55 55 55 00 00 
				00 00 55 55 55 55 55 55 00 00 
				FF FF FF FF FF FF FF FF FF FF";
							
			var rgbFrame1 = FrameGenerator.FromString(rgb1, rgb1, rgb1);
			var rgbFrame2 = FrameGenerator.FromString(rgb2, rgb2, rgb2);
			
			await AssertFrame(_source, dest, frame1, rgbFrame1);
			await AssertFrame(_source, dest, frame2, rgbFrame2);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Downscaled_Stretch()
		{
			var dest = new DestinationFixedRgb24(10, 4) { DmdAllowHdScaling = true };
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.Resize = ResizeMode.Stretch;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				3333333333
				3000000003
				3000000003
				3001111003
				3001111003
				3001111003
				3001111003
				3000000003
				3000000003
				3333333333");

			var rgbFrame = FrameGenerator.FromWhiteString(@"
				FF 66 66 66 66 66 66 66 66 FF 
				FF 00 00 4D 4D 4D 4D 00 00 FF 
				FF 00 00 44 44 44 44 00 00 FF 
				FF 66 66 66 66 66 66 66 66 FF");
							
			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Fit()
		{
			var dest = new DestinationFixedRgb24(16, 8);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003333001
				1003333001
				1111331111");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 55 55 55 55 FF FF 55 55 55 55 00 00 00 
				00 00 00 55 00 00 FF FF FF FF 00 00 55 00 00 00 
				00 00 00 55 00 00 FF FF FF FF 00 00 55 00 00 00 
				00 00 00 55 55 55 55 FF FF 55 55 55 55 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
							
			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Fill()
		{
			var dest = new DestinationFixedRgb24(16, 8);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fill;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003333001
				1003333001
				1111331111");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				55 55 55 55 55 6A EE FF FF 7B 55 55 55 55 55 55 
				55 34 2B 2B 77 B5 F7 FF FF BD 8A 2E 2B 30 51 55 
				55 13 00 00 99 FF FF FF FF FF BF 06 00 0B 4D 55 
				55 13 00 00 99 FF FF FF FF FF BF 06 00 0B 4D 55 
				55 13 00 00 99 FF FF FF FF FF BF 06 00 0B 4D 55 
				55 34 2B 2B 77 B5 F7 FF FF BD 8A 2E 2B 30 51 55 
				55 55 55 55 55 6A EE FF FF 7B 55 55 55 55 55 55 
				55 55 55 55 55 6A EE FF FF 7B 55 55 55 55 55 55");

			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Stretch()
		{
			var dest = new DestinationFixedRgb24(16, 8);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Stretch;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003333001
				1003333001
				1111331111");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				55 55 55 55 55 6A EE FF FF 7B 55 55 55 55 55 55 
				55 1E 0E 0E 8E E6 FC FF FF E9 AE 13 0E 17 4E 55 
				55 13 00 00 99 FF FF FF FF FF BF 06 00 0B 4D 55 
				55 13 00 00 99 FF FF FF FF FF BF 06 00 0B 4D 55 
				55 4A 47 47 60 83 F1 FF FF 91 67 48 47 49 54 55 
				55 55 55 55 55 6A EE FF FF 7B 55 55 55 55 55 55 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");

			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Fill_Same_Height()
		{
			var dest = new DestinationFixedRgb24(16, 4);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fill;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003333001
				1003223001
				1003223001
				1003333001
				1111331111");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 55 00 00 FF FF FF FF 00 00 55 00 00 00 
				00 00 00 55 00 00 FF AA AA FF 00 00 55 00 00 00 
				00 00 00 55 00 00 FF AA AA FF 00 00 55 00 00 00 
				00 00 00 55 00 00 FF FF FF FF 00 00 55 00 00 00");

			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Fit_Same_Height()
		{
			var dest = new DestinationFixedRgb24(16, 4);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003333001
				1003223001
				1003223001
				1003333001
				1111331111");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 00 00 4D 39 A3 FF 8C 39 51 00 00 00 00 
				00 00 00 00 00 3C 00 ED B8 E1 00 4A 00 00 00 00 
				00 00 00 00 00 3C 00 F0 C6 E2 00 4A 00 00 00 00 
				00 00 00 00 00 51 47 8D FF 75 47 53 00 00 00 00");

			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Stretch_Same_Height()
		{
			var dest = new DestinationFixedRgb24(16, 4);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Stretch;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003333001
				1003223001
				1003223001
				1003333001
				1111331111");

			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 55 39 39 8E FF FF 8E 39 39 55 00 00 00 
				00 00 00 55 00 00 FF B8 B8 FF 00 00 55 00 00 00 
				00 00 00 55 00 00 FF C6 C6 FF 00 00 55 00 00 00 
				00 00 00 55 47 47 71 FF FF 71 47 47 55 00 00 00");
							
			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Fill_Same_Width()
		{
			var dest = new DestinationFixedRgb24(8, 6);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fill;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003223001
				1003333001
				1111331111");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 00 00 00 00 00 
				55 55 55 FF FF 55 55 55 
				00 00 FF AA AA FF 00 00 
				00 00 FF FF FF FF 00 00 
				55 55 55 FF FF 55 55 55 
				00 00 00 00 00 00 00 00");

			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Fit_Same_Width()
		{
			var dest = new DestinationFixedRgb24(8, 6);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Fit;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003223001
				1003333001
				1111331111");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 00 00 00 00 00 
				51 40 73 E4 D5 5C 40 54 
				44 00 CC E4 E8 73 00 51 
				54 4E 5F F7 E0 57 4E 55 
				00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00");
							
			await AssertFrame(_source, dest, frame, rgbFrame);
		}
		
		[TestCase]
		public async Task Should_Convert_To_RGB24_Upscaled_Stretch_Same_Width()
		{
			var dest = new DestinationFixedRgb24(8, 6);
			
			_graph.Source = _source;
			_graph.SetColor(Colors.White);
			_graph.ScalerMode = ScalerMode.None;
			_graph.Resize = ResizeMode.Stretch;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();
			
			var frame = FrameGenerator.FromString(@"
				1111331111
				1003223001
				1003333001
				1111331111");
			
			var rgbFrame = FrameGenerator.FromWhiteString(@"
				00 00 00 00 00 00 00 00 
				55 55 55 F7 DD 55 55 55 
				44 00 CC AE BB 73 00 51 
				44 00 CC FF FF 73 00 51 
				55 55 55 F7 DD 55 55 55 
				00 00 00 00 00 00 00 00");
							
			await AssertFrame(_source, dest, frame, rgbFrame);
		}

		[TestCase]
		public async Task Should_Convert_To_Bitmap()
		{
			var dest = new DestinationBitmapFixed(8, 4);

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

	}
}
