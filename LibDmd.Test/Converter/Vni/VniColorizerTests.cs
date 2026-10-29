using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Converter.Vni;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class VniColorizerTests
	{
		private static readonly Dimensions Dim = new Dimensions(128, 32);
		private static readonly Color[] Gray4 = { Colors.Black, Colors.DarkGray, Colors.Gray, Colors.White };
		private static readonly Color[] Red4 = { Colors.Black, Colors.DarkRed, Colors.Red, Colors.Pink };

		/// <summary>
		/// Lets the tests feed the colorizer the frames its clock would.
		/// </summary>
		private class ClockedColorizer : VniColorizer
		{
			public readonly List<ColoredFrame> Frames = new List<ColoredFrame>();
			public readonly List<DmdFrame> Rgb24Frames = new List<DmdFrame>();

			public ClockedColorizer(PalFile palFile, AnimationSet animations) : base(palFile, animations)
			{
				GetColoredGray2Frames().Subscribe(f => Frames.Add((ColoredFrame)f.Clone()));
				GetColoredGray4Frames().Subscribe(f => Frames.Add((ColoredFrame)f.Clone()));
				GetColoredGray6Frames().Subscribe(f => Frames.Add((ColoredFrame)f.Clone()));
				GetRgb24Frames().Subscribe(f => Rgb24Frames.Add((DmdFrame)f.Clone()));
			}

			public void Tick(DmdFrame frame) => ConvertClocked(frame);
		}

		/// <summary>
		/// A 2-bit frame with the given pixels lit.
		/// </summary>
		private static DmdFrame Gray2Frame(params int[] pixels)
		{
			var data = new byte[Dim.Surface];
			foreach (var pixel in pixels) {
				data[pixel] = 1;
			}
			return new DmdFrame(Dim, data, 2);
		}

		private static uint Crc(DmdFrame frame) => FrameUtil.Checksum(frame.BitPlanes[0]);

		/// <summary>
		/// A 2-plane frame where only the pixel of the given index is lit, in color 3.
		/// </summary>
		private static FsqFrame AnimFrame(int pixel, uint delay = 0)
		{
			return new FsqFrame { Delay = delay, Planes = new[] { Planes.With(pixel), Planes.With(pixel) } };
		}

		private static int LitPixel(ColoredFrame frame) => Array.FindIndex(frame.Data, b => b == 3);

		[TestCase(false)]
		[TestCase(true)]
		public void ShouldRedrawHeldFrameWhenTimedPaletteExpires(bool holdTrigger)
		{
			var trigger = Gray2Frame(1);
			var held = holdTrigger ? trigger : Gray2Frame(2);
			var pal = new PalBuilder(1).Palette(0, 1, Gray4).Palette(1, 0, Red4)
				.Mapping(Crc(trigger), SwitchMode.Palette, 1, 200).BuildFile();
			using (var colorizer = new ClockedColorizer(pal, null)) {
				colorizer.Tick(trigger);
				colorizer.Tick(held);
				colorizer.Frames.Last().Palette.Should().Equal(Red4);
				System.Threading.SpinWait.SpinUntil(() => {
					colorizer.Tick(held);
					return colorizer.Frames.Last().Palette.SequenceEqual(Gray4);
				}, 2000).Should().BeTrue("the expired palette must repaint unchanged input");
				colorizer.Frames.Last().Data.Should().Equal(held.Data);
				colorizer.Tick(held);
				colorizer.Frames.Last().Palette.Should().Equal(Gray4);
			}
		}

		[TestCase(SwitchMode.Replace, 2)]
		[TestCase(SwitchMode.ReplaceNoRetrigger, 2)]
		[TestCase(SwitchMode.FollowReplace, 2)]
		[TestCase(SwitchMode.ColorMask, 2)]
		[TestCase(SwitchMode.ColorMaskLoop, 2)]
		[TestCase(SwitchMode.Follow, 2)]
		[TestCase(SwitchMode.LayeredColorMask, 2)]
		[TestCase(SwitchMode.MaskedReplace, 2)]
		[TestCase(SwitchMode.Replace, 15)]
		[TestCase(SwitchMode.Replace, 24)]
		public void ShouldCenterSmallFsqAnimationsInPaddedInput(SwitchMode mode, int depth)
		{
			var trigger = Gray2Frame(8 * 128 + 1);
			var plane = new byte[256];
			plane[0] = 1;
			plane[255] = 128;
			var layered = mode == SwitchMode.LayeredColorMask || mode == SwitchMode.MaskedReplace;
			var fsq = new FsqBuilder(layered ? 3 : 2);
			var displayFrame = new FsqFrame {
				Delay = 50,
				Mask = Enumerable.Repeat((byte)255, 256).ToArray(),
				Planes = Enumerable.Range(0, depth).Select(_ => plane).ToArray()
			};
			var hashes = new byte[256];
			Buffer.BlockCopy(new[] { Crc(trigger) }, 0, hashes, 0, 4);
			var offset = layered
				? fsq.Animation(new FsqFrame { Planes = new[] { new byte[256], hashes } }, displayFrame)
				: fsq.Animation(displayFrame);
			var colors = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var pal = new PalBuilder(1).Palette(0, 1, colors).Mapping(Crc(trigger), mode, 0, offset).BuildFile();
			using (var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "small.fsq", pal))) {
				colorizer.Tick(trigger);
				if (depth > 6) {
					var output = colorizer.Rgb24Frames.Single();
					output.Dimensions.Should().Be(Dim);
					output.Data.Take(8 * 128 * 3).Should().OnlyContain(b => b == 0);
					output.Data.Skip(24 * 128 * 3).Should().OnlyContain(b => b == 0);
					output.Data[8 * 128 * 3].Should().Be((byte)(depth == 15 ? 248 : 255));
					output.Data[(24 * 128 - 1) * 3].Should().Be(output.Data[8 * 128 * 3]);
				} else {
					var output = colorizer.Frames.Single();
					output.Dimensions.Should().Be(Dim);
					output.Data.Take(8 * 128).Should().OnlyContain(b => b == 0);
					output.Data.Skip(24 * 128).Should().OnlyContain(b => b == 0);
					output.Data[8 * 128].Should().Be((byte)(FsqFrameSeq.IsColorMaskMode(mode) ? 12 : 3));
					output.Data[24 * 128 - 1].Should().Be(output.Data[8 * 128]);
				}
			}
		}

		private static ClockedColorizer ReplaceColorizer(SwitchMode mode, DmdFrame trigger, int numFrames)
		{
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(Enumerable.Range(0, numFrames).Select(i => AnimFrame(100 + i)).ToArray());
			var pal = new PalBuilder(1).Palette(0, 1, Gray4).Mapping(Crc(trigger), mode, 0, offset).BuildFile();
			return new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal));
		}

		[TestCase(SwitchMode.Replace)]
		[TestCase(SwitchMode.ReplaceNoRetrigger)]
		public void ShouldPlayReplaceAnimationWhileTriggerFrameIsRepeated(SwitchMode mode)
		{
			var trigger = Gray2Frame(1);
			var colorizer = ReplaceColorizer(mode, trigger, 3);

			// the clock repeats the frame, and VPM may send it again.
			colorizer.Tick(trigger);
			colorizer.Tick(trigger);
			colorizer.Tick(Gray2Frame(1));

			colorizer.Frames.Select(LitPixel).Should().Equal(100, 101, 102);
		}

		[Test]
		public void ShouldKeepT2LogoBlueWhileItsTriggerFrameIsRepeated()
		{
			// T2's IntroLogo19 is a one-frame, 6-plane color mask with a 38 ms delay.
			// The ROM holds its trigger longer than that delay (1,057 ms in the recording).
			var trigger = Gray2Frame(1);
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(new FsqFrame {
				Delay = 38,
				Planes = new[] { Planes.With(1), Planes.Empty(), Planes.Empty(), Planes.Empty() }
			});
			var blue64 = Enumerable.Range(0, 64).Select(i => Color.FromRgb(0, 0, (byte)(i * 4))).ToArray();
			var pal = new PalBuilder(1).Palette(0, 1, Gray4).Palette(1, 0, blue64)
				.Mapping(Crc(trigger), SwitchMode.ColorMask, 1, offset).BuildFile();
			using (var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "t2-logo.fsq", pal))) {
				colorizer.Tick(trigger);
				System.Threading.Thread.Sleep(100);
				colorizer.Tick(trigger);
				colorizer.Tick(Gray2Frame(1));

				colorizer.Frames.Should().ContainSingle();
				var logo = colorizer.Frames.Last();
				logo.BitLength.Should().Be(6);
				logo.Palette[logo.Data[1]].Should().Be(blue64[5]);

				// A different input can expire the mask and restore the default palette.
				colorizer.Tick(Gray2Frame(2));
				colorizer.Frames.Last().BitLength.Should().Be(2);
				colorizer.Frames.Last().Palette.Should().Equal(Gray4);
			}
		}

		[TestCase(SwitchMode.Replace)]
		[TestCase(SwitchMode.ReplaceNoRetrigger)]
		[TestCase(SwitchMode.ColorMaskLoop)]
		public void ShouldHoldSingleFrameAnimationOnRepeatedInput(SwitchMode mode)
		{
			var trigger = Gray2Frame(1);
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(new FsqFrame {
				Planes = new[] { Planes.With(100), Planes.Empty() }
			});
			var colors = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var pal = new PalBuilder(1).Palette(0, 1, colors).Mapping(Crc(trigger), mode, 0, offset).BuildFile();
			using (var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal))) {
				colorizer.Tick(trigger);
				colorizer.Tick(trigger);
				colorizer.Tick(trigger);
				colorizer.Frames.Should().ContainSingle();
			}
		}

		[TestCase(SwitchMode.Replace)]
		[TestCase(SwitchMode.ReplaceNoRetrigger)]
		public void ShouldKeepLastReplaceOutputWhenItFinishesOnRepeatedInput(SwitchMode mode)
		{
			var trigger = Gray2Frame(1);
			using (var colorizer = ReplaceColorizer(mode, trigger, 2)) {
				colorizer.Tick(trigger);
				colorizer.Tick(trigger);
				colorizer.Tick(trigger);
				colorizer.Tick(trigger);
				colorizer.Frames.Select(LitPixel).Should().Equal(100, 101);
				colorizer.Tick(Gray2Frame(2));
				colorizer.Frames.Select(LitPixel).Should().Equal(100, 101, -1);
			}
		}

		[Test]
		public void ShouldOnlyAdvanceMultiFrameColorMaskOnChangedInput()
		{
			var trigger = Gray2Frame(1);
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(
				new FsqFrame { Planes = new[] { Planes.With(100), Planes.Empty() } },
				new FsqFrame { Planes = new[] { Planes.With(101), Planes.Empty() } });
			var colors = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var pal = new PalBuilder(1).Palette(0, 1, colors).Mapping(Crc(trigger), SwitchMode.ColorMask, 0, offset).BuildFile();
			using (var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal))) {
				colorizer.Tick(trigger);
				colorizer.Tick(trigger);
				colorizer.Tick(Gray2Frame(2));
				colorizer.Tick(Gray2Frame(2));
				colorizer.Frames.Select(f => Array.FindIndex(f.Data, b => (b & 4) != 0)).Should().Equal(100, 101);
			}
		}

		[Test]
		public void ShouldLoopColorMaskLoopAnimation()
		{
			var trigger = Gray2Frame(1);
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(
				new FsqFrame { Planes = new[] { Planes.With(100), Planes.Empty() } },
				new FsqFrame { Planes = new[] { Planes.With(101), Planes.Empty() } });
			var colors = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var pal = new PalBuilder(1).Palette(0, 1, colors).Mapping(Crc(trigger), SwitchMode.ColorMaskLoop, 0, offset).BuildFile();
			var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal));

			for (var i = 0; i < 5; i++) {
				colorizer.Tick(trigger);
			}

			// the added planes become bit 2 of the 4-bit frame, and the input stays below.
			colorizer.Frames.Should().HaveCount(5);
			colorizer.Frames.Select(f => Array.FindIndex(f.Data, b => b == 4)).Should().Equal(100, 101, 100, 101, 100);
			colorizer.Frames.Should().OnlyContain(f => f.Data[1] == 1);
		}

		[TestCase(SwitchMode.Replace, 100)]
		[TestCase(SwitchMode.ReplaceNoRetrigger, 102)]
		public void ShouldRestartOnRetriggerOnlyWhenAllowed(SwitchMode mode, int expectedPixel)
		{
			var trigger = Gray2Frame(1);
			var colorizer = ReplaceColorizer(mode, trigger, 4);

			colorizer.Tick(trigger);
			colorizer.Tick(Gray2Frame(2));
			colorizer.Tick(Gray2Frame(1));

			colorizer.Frames.Select(LitPixel).Should().Equal(100, 101, expectedPixel);
		}

		[Test]
		public void ShouldOutputRgbAnimations()
		{
			var trigger = Gray2Frame(1);
			const int pixel = 5;
			var color = Color.FromRgb(0x12, 0x34, 0x56);
			var planes = new List<byte[]>();
			foreach (var channel in new[] { color.B, color.G, color.R }) {
				for (var bit = 0; bit < 8; bit++) {
					planes.Add((channel & (1 << bit)) != 0 ? Planes.With(pixel) : Planes.Empty());
				}
			}
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(new FsqFrame { Mask = Planes.Empty(), Planes = planes.ToArray() });
			var pal = new PalBuilder(1).Palette(0, 1, Gray4).Mapping(Crc(trigger), SwitchMode.Replace, 0, offset).BuildFile();
			var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal));

			colorizer.Tick(trigger);

			colorizer.Rgb24Frames.Should().ContainSingle();
			var rgb = colorizer.Rgb24Frames[0];
			rgb.Dimensions.Should().Be(Dim);
			rgb.Data.Skip(pixel * 3).Take(3).Should().Equal(0x12, 0x34, 0x56);
			rgb.Data.Take(3).Should().Equal(0, 0, 0);
		}

		[Test]
		public void ShouldOutputRgb15Animations()
		{
			var planes = new byte[15][];
			for (var i = 0; i < 15; i++) {
				planes[i] = Planes.With(0);
			}

			var rgb = VniColorizer.PlanesToRgb24(Dim, planes);

			rgb.Take(6).Should().Equal(0xf8, 0xf8, 0xf8, 0, 0, 0);
		}

		[TestCase(15)]
		[TestCase(24)]
		public void ShouldRenderRgbAnimationToAllColorDestinations(int bitDepth)
		{
			var trigger = Gray2Frame(1);
			const int pixel = 5;
			var bitsPerChannel = bitDepth / 3;
			var planes = new List<byte[]>();
			foreach (var channel in new byte[] { 0x58, 0x38, 0x18 }) {
				for (var bit = 0; bit < bitsPerChannel; bit++) {
					planes.Add((channel & (1 << (bit + 8 - bitsPerChannel))) != 0 ? Planes.With(pixel) : Planes.Empty());
				}
			}
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(new FsqFrame { Mask = Planes.Empty(), Planes = planes.ToArray() });
			var pal = new PalBuilder(1).Palette(0, 1, Gray4).Mapping(Crc(trigger), SwitchMode.Replace, 0, offset).BuildFile();
			var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "rgb.fsq", pal));
			var bitmapDest = new DestinationFixedBitmap(128, 32);
			var rgb565Dest = new DestinationFixedRgb565(128, 32);
			var rgb24Dest = new DestinationFixedRgb24(128, 32);
			BmpFrame bitmap = null;
			DmdFrame rgb565 = null, rgb24 = null;
			bitmapDest.Frame.Subscribe(f => bitmap = f);
			rgb565Dest.Frame.Subscribe(f => rgb565 = (DmdFrame)f.Clone());
			rgb24Dest.Frame.Subscribe(f => rgb24 = (DmdFrame)f.Clone());
			using (var graph = new RenderGraph(new UndisposedReferences(), true) {
				Source = new SourceGray2(),
				Converter = colorizer,
				Destinations = new List<IDestination> { bitmapDest, rgb565Dest, rgb24Dest }
			}) {
				graph.StartRendering();
				colorizer.Tick(trigger);

				bitmap.Should().NotBeNull("GIF and bitmap destinations must receive RGB animations");
				rgb565.Should().NotBeNull();
				rgb24.Should().NotBeNull();
				bitmap.ConvertToRgb24().Data.Skip(pixel * 3).Take(3).Should().Equal(0x18, 0x38, 0x58);
				rgb24.Data.Skip(pixel * 3).Take(3).Should().Equal(0x18, 0x38, 0x58);
				rgb565.Data.Should().Equal(((DmdFrame)rgb24.Clone()).ConvertRgb24ToRgb565().Data);
			}
		}

		[Test]
		public void ShouldNotLetEventMappingsHideMaskedMappings()
		{
			var trigger = Gray2Frame(1, 2);
			var mask = Planes.With(1);
			var maskedCrc = FrameUtil.ChecksumWithMask(trigger.BitPlanes[0], mask);
			var pal = new PalBuilder(1)
				.Palette(0, 1, Gray4)
				.Palette(1, 0, Red4)
				.Mapping(Crc(trigger), SwitchMode.Event, 0, 42)
				.Mapping(maskedCrc, SwitchMode.Palette, 1, 0)
				.Mask(mask)
				.BuildFile();
			var colorizer = new ClockedColorizer(pal, null);

			colorizer.Tick(trigger);

			colorizer.Frames.Last().Palette[3].Should().Be(Colors.Pink);
		}

		[Test]
		public void ShouldKeepEmbeddedPaletteWhileMarkerFrameIsRepeated()
		{
			var colors0 = Enumerable.Range(0, 16).Select(i => Color.FromRgb(0, 0, (byte)i)).ToArray();
			var colors1 = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var pal = new PalBuilder(1).Palette(0, 2, colors0).Palette(1, 0, colors1).BuildFile();
			var colorizer = new ClockedColorizer(pal, null);
			var data = new byte[Dim.Surface];
			data[0] = 8; data[1] = 9; data[2] = 10; data[3] = 11; data[4] = 1;
			data[200] = 15;
			var frame = new DmdFrame(Dim, data, 4);

			colorizer.Tick(frame);
			colorizer.Tick(frame);
			colorizer.Tick(frame);

			colorizer.Frames.Should().ContainSingle();
			colorizer.Frames[0].Palette[15].Should().Be(colors1[15]);
		}

		[Test]
		public void ShouldTriggerAgainWhenFrameChanges()
		{
			var trigger = Gray2Frame(1);
			var colorizer = ReplaceColorizer(SwitchMode.Replace, trigger, 1);

			colorizer.Tick(trigger);
			colorizer.Tick(trigger);
			colorizer.Tick(Gray2Frame(2));
			colorizer.Tick(trigger);

			// one-frame animation, then the live frame, then the animation again
			colorizer.Frames.Select(LitPixel).Should().Equal(100, -1, 100);
		}

		[TestCase(SwitchMode.ColorMask)]
		[TestCase(SwitchMode.ColorMaskLoop)]
		public void ShouldAdvanceColorMaskWhenFrameChanges(SwitchMode mode)
		{
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(
				new FsqFrame { Delay = 10000, Planes = new[] { Planes.With(100), Planes.Empty() } },
				new FsqFrame { Delay = 10000, Planes = new[] { Planes.With(101), Planes.Empty() } });
			var colors = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var trigger = Gray2Frame(1);
			var pal = new PalBuilder(1).Palette(0, 1, colors).Mapping(Crc(trigger), mode, 0, offset).BuildFile();
			var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal));

			colorizer.Tick(trigger);
			colorizer.Tick(trigger);
			colorizer.Tick(Gray2Frame(2));
			colorizer.Tick(Gray2Frame(3));

			// the delays are long, so only the new frames move the animation on.
			colorizer.Frames.Select(f => Array.FindIndex(f.Data, b => (b & 4) != 0)).Should().Equal(100, 101, mode == SwitchMode.ColorMaskLoop ? 100 : -1);
		}

		[Test]
		public void ShouldGiveFrameItsFullDelayAfterFrameChange()
		{
			var fsq = new FsqBuilder(2);
			var offset = fsq.Animation(
				new FsqFrame { Delay = 1000, Planes = new[] { Planes.With(100), Planes.Empty() } },
				new FsqFrame { Delay = 1000, Planes = new[] { Planes.With(101), Planes.Empty() } });
			var colors = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var trigger = Gray2Frame(1);
			var pal = new PalBuilder(1).Palette(0, 1, colors).Mapping(Crc(trigger), SwitchMode.ColorMaskLoop, 0, offset).BuildFile();
			var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "test.fsq", pal));
			var next = Gray2Frame(2);

			colorizer.Tick(trigger);
			System.Threading.Thread.Sleep(600);
			colorizer.Tick(next);
			System.Threading.Thread.Sleep(500);
			colorizer.Tick(next);

			// the second frame started with the new frame, so it's still showing.
			colorizer.Frames.Select(f => Array.FindIndex(f.Data, b => (b & 4) != 0)).Should().Equal(100, 101);
		}

		[Test]
		public void ShouldResetEmbeddedPaletteWhenMarkerDisappears()
		{
			var colors0 = Enumerable.Range(0, 16).Select(i => Color.FromRgb(0, 0, (byte)i)).ToArray();
			var colors1 = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var pal = new PalBuilder(1).Palette(0, 2, colors0).Palette(1, 0, colors1).BuildFile();
			var colorizer = new ClockedColorizer(pal, null);
			var marked = new byte[Dim.Surface];
			marked[0] = 8; marked[1] = 9; marked[2] = 10; marked[3] = 11; marked[4] = 1;
			marked[200] = 15;
			var unmarked = new byte[Dim.Surface];
			unmarked[200] = 15;

			colorizer.Tick(new DmdFrame(Dim, marked, 4));
			colorizer.Tick(new DmdFrame(Dim, unmarked, 4));

			colorizer.Frames.Select(f => f.Palette[15]).Should().Equal(colors1[15], colors0[15]);
		}

		[Test]
		public void ShouldTreatSameBytesWithOtherBitLengthAsNewFrame()
		{
			var colors0 = Enumerable.Range(0, 16).Select(i => Color.FromRgb(0, 0, (byte)i)).ToArray();
			var colors1 = Enumerable.Range(0, 16).Select(i => Color.FromRgb((byte)i, 0, 0)).ToArray();
			var pal = new PalBuilder(2).Palette(0, 1, colors0).Palette(1, 0, colors1)
				.Mapping(FrameUtil.Checksum(Planes.Empty()), SwitchMode.Palette, 1, 0).BuildFile();
			var colorizer = new ClockedColorizer(pal, null);
			var data = Enumerable.Repeat((byte)3, Dim.Surface).ToArray();

			colorizer.Tick(new DmdFrame(Dim, data, 2));
			colorizer.Tick(new DmdFrame(Dim, (byte[])data.Clone(), 4));

			// as 4-bit frame, the upper planes are empty, which triggers the palette.
			colorizer.Frames.Last().Palette[15].Should().Be(colors1[15]);
		}
	}
}
