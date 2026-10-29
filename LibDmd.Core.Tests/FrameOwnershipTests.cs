using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using LibDmd.Common;
using LibDmd.Converter;
using LibDmd.Converter.Vni;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using LibDmd.Input;
using LibDmd.Input.Passthrough;
using LibDmd.Output;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class FrameOwnershipTests
	{
		[Test]
		public void NestedCallbacksKeepEveryLiveBufferSeparate()
		{
			var pool = new FrameBufferPool();
			using (pool.Enter()) {
				var original = FrameBufferPool.Allocate<byte>(4096);
				original[0] = 1;
				var intermediate = FrameBufferPool.Allocate<byte>(4096);
				intermediate[0] = 2;
				using (pool.Enter()) {
					var nested = FrameBufferPool.Allocate<byte>(4096);
					nested[0] = 3;
					Assert.That(nested, Is.Not.SameAs(original).And.Not.SameAs(intermediate));
				}
				Assert.That(original[0], Is.EqualTo(1));
				Assert.That(intermediate[0], Is.EqualTo(2));
			}
		}

		[TestCase(2)]
		[TestCase(4)]
		[TestCase(6)]
		public void PackedPlanesRoundTripAndStayInsideDestination(int bits)
		{
			var dim = new Dimensions(16, 4);
			var data = Enumerable.Range(0, dim.Surface).Select(i => (byte)(i % (1 << bits))).ToArray();
			var planes = FrameUtil.Split(dim, bits, data);
			var expected = planes.SelectMany(p => p).ToArray();
			var packed = Enumerable.Repeat((byte)0xCD, expected.Length + 10).ToArray();
			new DmdFrame(dim, data, bits).CopyPlanesTo(packed, 5);
			Assert.That(packed.Take(5), Is.All.EqualTo(0xCD));
			Assert.That(packed.Skip(5).Take(expected.Length), Is.EqualTo(expected));
			Assert.That(packed.Skip(5 + expected.Length), Is.All.EqualTo(0xCD));
			var joined = Enumerable.Repeat((byte)255, dim.Surface).ToArray();
			FrameUtil.Join(dim, planes, joined);
			Assert.That(joined, Is.EqualTo(data));
		}

		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void PackedFlipMatchesPixelFlip(bool horizontal, bool vertical)
		{
			var dim = new Dimensions(16, 4);
			var data = Enumerable.Range(0, dim.Surface).Select(i => (byte)((i * 17 + 5) % 64)).ToArray();
			var expected = TransformationUtil.Flip(dim, 1, data, horizontal, vertical);
			var planes = FrameUtil.Split(dim, 6, data);
			Assert.That(FrameUtil.Join(dim, TransformationUtil.Flip(dim, planes, horizontal, vertical)), Is.EqualTo(expected));
		}

		[Test]
		public void AsyncQueueCopiesPixelsPaletteAndDimensionsBeforeProducerReusesThem()
		{
			using var entered = new AutoResetEvent(false);
			using var release = new AutoResetEvent(false);
			using var finished = new AutoResetEvent(false);
			var received = new List<ColoredFrame>();
			using var observer = new BufferedFrameObserver<ColoredFrame, ColoredFrame>(f => f, f => {
				if (received.Count == 0) { entered.Set(); release.WaitOne(5000); }
				var copy = new ColoredFrame();
				copy.Update(f);
				received.Add(copy);
				if (received.Count == 3) finished.Set();
			}, false, 0, null, null);
			var frame = new ColoredFrame(new Dimensions(8, 1), new byte[8], new[] { Colors.Black, Colors.Red, Colors.Green, Colors.White });
			frame.Data[0] = 1;
			observer.OnNext(frame);
			Assert.That(entered.WaitOne(5000), Is.True);
			frame.Data[0] = 2;
			frame.Palette[1] = Colors.Blue;
			observer.OnNext(frame);
			frame.Update(new Dimensions(16, 1), new byte[16], 2);
			frame.Data[0] = 3;
			observer.OnNext(frame);
			frame.Data[0] = 0;
			frame.Palette[1] = Colors.Black;
			release.Set();
			Assert.That(finished.WaitOne(5000), Is.True);
			Assert.That(received.Select(f => f.Data[0]), Is.EqualTo(new byte[] { 1, 2, 3 }));
			Assert.That(received.Select(f => f.Dimensions.Width), Is.EqualTo(new[] { 8, 8, 16 }));
			Assert.That(received.Select(f => f.Palette[1]), Is.EqualTo(new[] { Colors.Red, Colors.Blue, Colors.Blue }));
		}

		[Test]
		public void RawAndAlphanumericSnapshotsPreserveAllPlanesAndSegments()
		{
			var raw = new RawFrame().Update(new Dimensions(8, 1), new byte[8], new[] { new byte[] { 1 }, new byte[] { 2 } }, new[] { new byte[] { 4 } });
			var copy = (RawFrame)new FrameSnapshot<DmdFrame>().Copy(raw);
			raw.RawPlanes[0][0] = 8;
			raw.ExtraRawPlanes[0][0] = 16;
			Assert.That(copy.RawPlanes[0][0], Is.EqualTo(1));
			Assert.That(copy.ExtraRawPlanes[0][0], Is.EqualTo(4));
			var alpha = new AlphaNumericFrame(default, new ushort[] { 1 }, new ushort[] { 2 });
			var alphaCopy = new FrameSnapshot<AlphaNumericFrame>().Copy(alpha);
			alpha.SegmentData[0] = 3;
			alpha.SegmentDataExtended[0] = 4;
			Assert.That(alphaCopy.SegmentData[0], Is.EqualTo(1));
			Assert.That(alphaCopy.SegmentDataExtended[0], Is.EqualTo(2));
		}

		[TestCase(true)]
		[TestCase(false)]
		public void ColorConversionDoesNotModifyTheFrameSeenByLaterDestinations(bool reuse)
		{
			using var format = new BehaviorSubject<FrameFormat>(FrameFormat.Gray2);
			var source = new PassthroughGray2Source(format);
			var rgb = new CaptureRgb();
			var gray = new CaptureGray();
			using var graph = new RenderGraph(new UndisposedReferences(), true) {
				Source = source, Destinations = new List<IDestination> { rgb, gray }, ReuseFrameBuffers = reuse
			};
			using var rendering = graph.StartRendering();
			var input = new DmdFrame(Dimensions.Standard, new byte[4096], 2);
			input.Data[0] = 3;
			var producer = new FrameBufferPool();
			using (producer.Enter()) source.NextFrame(FrameBufferPool.CloneForRendering(input));
			Assert.That(gray.Pixel, Is.EqualTo(3));
			Assert.That(gray.Bits, Is.EqualTo(2));
			Assert.That(rgb.Frame.BitLength, Is.EqualTo(24));
			Assert.That(input.Data[0], Is.EqualTo(3));
			Assert.That(input.BitLength, Is.EqualTo(2));
			if (!reuse) {
				var first = rgb.Frame;
				var firstData = (byte[])first.Data.Clone();
				input.Data[0] = 0;
				using (producer.Enter()) source.NextFrame(FrameBufferPool.CloneForRendering(input));
				Assert.That(first.Data, Is.EqualTo(firstData), "owning graphs cannot inherit a producer's scratch scope");
			}
		}

		[Test]
		public void PaletteRotationRetainsItsOwnPixelsAndNeverEditsTheSourcePalette()
		{
			var source = new RotationSource();
			using var wrapper = new ColorRotationWrapper(source, source, true);
			byte pixel = 0;
			using var subscription = wrapper.GetRgb24Frames().Subscribe(f => pixel = f.Data[0]);
			var palette = new Color[64];
			palette[1] = Colors.Red;
			var frame = new ColoredFrame(new Dimensions(8, 1), new byte[8], palette);
			frame.Data[0] = 1;
			source.Frames.OnNext(frame);
			Array.Clear(frame.Data, 0, 8);
			var rotation = new Color[64]; rotation[1] = Colors.White;
			source.Palettes.OnNext(rotation);
			Assert.That(pixel, Is.EqualTo(255));
			Assert.That(frame.Palette[1], Is.EqualTo(Colors.Red));
		}

		[TestCase(16)]
		[TestCase(32)]
		public void ClockedConverterRetainsPaddedPixelsAndPreservesTheJoinedInputFormat(int height)
		{
			using var converter = new TestClockedConverter();
			var dim = new Dimensions(128, height);
			var data = new byte[dim.Surface]; data[1] = 3;
			var planes = FrameUtil.Split(dim, 2, data);
			var extra = new byte[dim.Surface / 8]; extra[0] = 4;
			var source = new RawFrame().Update(dim, data, planes, new[] { extra });
			converter.Convert(source);
			data[1] = 0;
			planes[0][0] = 0;
			extra[0] = 0;
			converter.TickNow();
			var retained = converter.Received;
			Assert.That(retained, Is.Not.InstanceOf<RawFrame>());
			Assert.That(source.Dimensions, Is.EqualTo(dim));
			Assert.That(retained.Dimensions, Is.EqualTo(Dimensions.Standard));
			Assert.That(retained.Data[(32 - height) / 2 * 128 + 1], Is.EqualTo(3));
		}

		[Test]
		public void ClockedVniStillMatchesJoinedPixelsWhenRawPlanesHaveDifferentHashes()
		{
			var pixels = new byte[4096]; pixels[0] = 1;
			var joined = new DmdFrame(Dimensions.Standard, pixels, 2);
			var rawPlanes = new[] { Planes.With(7), Planes.Empty() };
			var gray = new[] { Colors.Black, Colors.White, Colors.White, Colors.White };
			var red = new[] { Colors.Black, Colors.Red, Colors.Red, Colors.Red };
			var blue = new[] { Colors.Black, Colors.Blue, Colors.Blue, Colors.Blue };
			var pal = new PalBuilder(1).Palette(0, 1, gray).Palette(1, 0, red).Palette(2, 0, blue)
				.Mapping(FrameUtil.Checksum(joined.BitPlanes[0]), SwitchMode.Palette, 1, 0)
				.Mapping(FrameUtil.Checksum(rawPlanes[0]), SwitchMode.Palette, 2, 0).BuildFile();
			using var colorizer = new VniColorizer(pal, null);
			var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
			((IDisposable)typeof(AbstractConverter).GetField("_clock", flags).GetValue(colorizer)).Dispose();
			var tick = (Action<long>)typeof(AbstractConverter).GetMethod("Tick", flags).CreateDelegate(typeof(Action<long>), colorizer);
			Color selected = Colors.Black;
			using var subscription = colorizer.GetColoredGray2Frames().Subscribe(f => selected = f.Palette[1]);
			colorizer.Convert(new RawFrame().Update(Dimensions.Standard, pixels, rawPlanes, Array.Empty<byte[]>()));
			tick(0);
			Assert.That(selected, Is.EqualTo(Colors.Red));
		}

		private sealed class TestClockedConverter : AbstractConverter
		{
			public DmdFrame Received;
			private readonly Action<long> _tick;
			public override string Name => "Clock test";
			public override IEnumerable<FrameFormat> From => new[] { FrameFormat.Gray2 };
			protected override bool PadSmallFrames => true;
			public override bool Supports(FrameFormat format) => true;
			public TestClockedConverter() : base(true)
			{
				var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
				((IDisposable)typeof(AbstractConverter).GetField("_clock", flags).GetValue(this)).Dispose();
				_tick = (Action<long>)typeof(AbstractConverter).GetMethod("Tick", flags).CreateDelegate(typeof(Action<long>), this);
			}
			protected override void ConvertClocked(DmdFrame frame) => Received = frame;
			public void TickNow() => _tick(0);
		}

		private class Destination : IDestination
		{
			public string Name => "Capture";
			public bool IsAvailable => true;
			public bool NeedsDuplicateFrames => true;
			public bool NeedsIdentificationFrames => false;
			public void ClearDisplay() { }
			public void Dispose() { }
		}
		private sealed class CaptureRgb : Destination, IRgb24Destination
		{
			public DmdFrame Frame;
			public void RenderRgb24(DmdFrame frame) => Frame = frame;
			public void SetColor(Color color) { }
			public void ClearColor() { }
			public void SetPalette(Color[] palette) { }
			public void ClearPalette() { }
		}
		private sealed class CaptureGray : Destination, IGray2Destination
		{
			public byte Pixel; public int Bits;
			public void RenderGray2(DmdFrame frame) { Pixel = frame.Data[0]; Bits = frame.BitLength; }
		}
		private sealed class RotationSource : IColoredGray6Source, IColorRotationSource
		{
			public string Name => "Rotating";
			public IObservable<System.Reactive.Unit> OnResume => null;
			public IObservable<System.Reactive.Unit> OnPause => null;
			public readonly Subject<ColoredFrame> Frames = new Subject<ColoredFrame>();
			public readonly Subject<Color[]> Palettes = new Subject<Color[]>();
			public IObservable<ColoredFrame> GetColoredGray6Frames() => Frames;
			public IObservable<Color[]> GetPaletteChanges() => Palettes;
		}
	}
}
