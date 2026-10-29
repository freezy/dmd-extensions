using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Reflection;
using LibDmd.Converter;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.Pixelcade;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LibDmd.Common;
using LibDmd.Converter.Vni;
using LibDmd.Frame;
using LibDmd.Input.Passthrough;
using LibDmd.Output;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class AllocationTests
	{
		private static readonly Dimensions Dim = Dimensions.Standard;
		private static readonly Color[] Palette = { Colors.Black, Color.FromRgb(128, 0, 0), Colors.Red, Colors.White };

		internal static void AssertNoAllocations(Action process)
		{
			for (var i = 0; i < 100; i++) process();
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 1000; i++) process();
			var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
			TestContext.WriteLine($"{allocated / 1000.0} bytes/frame");
			Assert.That(allocated, Is.Zero);
		}

		[Test]
		public void SplitIntoExistingPlanesDoesNotAllocate()
		{
			var frame = new byte[Dim.Surface];
			var planes = FrameUtil.Split(Dim, 4, frame);
			AssertNoAllocations(() => FrameUtil.Split(Dim, 4, frame, planes));
		}

		[Test]
		public void ColoredFrameSnapshotDoesNotAllocate()
		{
			var source = new ColoredFrame(Dim, new byte[Dim.Surface], Palette);
			var snapshot = new ColoredFrame();
			AssertNoAllocations(() => snapshot.Update(source));
		}

		[Test]
		public void VniColorizationDoesNotAllocate()
		{
			var pal = new PalBuilder(1).Palette(0, 1, Palette).BuildFile();
			using (var colorizer = new ClockedColorizer(pal) { ReuseFrameBuffers = true }) {
				var frame = new DmdFrame(Dim, new byte[Dim.Surface], 2);
				var count = 0;
				using (colorizer.GetColoredGray2Frames().Subscribe(_ => count++)) {
					AssertNoAllocations(() => {
						frame.Data[0] ^= 1;
						colorizer.Tick(frame);
					});
				}
				Assert.That(count, Is.EqualTo(1100));
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void SynchronousRenderGraphDoesNotAllocate(bool rgb)
		{
			using (var format = new BehaviorSubject<FrameFormat>(FrameFormat.Gray2)) {
				var source = new PassthroughGray2Source(format);
				var destination = rgb ? (CountingDestination)new RgbDestination() : new GrayDestination();
				using (var graph = new RenderGraph(new UndisposedReferences(), true) {
					Source = source, Destinations = new List<IDestination> { destination }, ReuseFrameBuffers = true
				})
				using (graph.StartRendering()) {
					var frame = new DmdFrame(Dim, new byte[Dim.Surface], 2);
					AssertNoAllocations(() => {
						frame.Data[0] ^= 1;
						source.NextFrame(frame);
					});
					Assert.That(destination.Count, Is.EqualTo(1100));
				}
			}
		}

		[TestCase(2, ScalerMode.Doubler)]
		[TestCase(4, ScalerMode.Scale2x)]
		[TestCase(6, ScalerMode.Scale2x)]
		[TestCase(8, ScalerMode.Doubler)]
		[TestCase(16, ScalerMode.Scale2x)]
		[TestCase(24, ScalerMode.Scale2x)]
		public void ConversionAndScalingReuseSeparateBuffers(int bits, ScalerMode scaler)
		{
			var data = new byte[Dim.Surface * (bits <= 8 ? 1 : bits / 8)];
			for (var i = 0; i < data.Length; i++) data[i] = (byte)(i % (bits < 8 ? 1 << bits : 256));
			var source = new DmdFrame(Dim, data, bits);
			var original = (byte[])data.Clone();
			var expectedFrame = (DmdFrame)source.Clone();
			if (bits != 2) expectedFrame.ConvertToGray2();
			var expected = expectedFrame.TransformHdScaling(scaler).Data;
			// Gray2 is already in the target format.
			var pool = new FrameBufferPool();
			byte[] result = null;
			AssertNoAllocations(() => {
				using (pool.Enter()) {
					var frame = FrameBufferPool.CloneForRendering(source);
					if (bits != 2) frame.ConvertToGray2();
					frame.TransformHdScaling(scaler);
					result = TransformationUtil.Flip(frame.Dimensions, 1, frame.Data, true, true);
				}
			});
			Assert.That(result, Is.EqualTo(TransformationUtil.Flip(Dim * 2, 1, expected, true, true)));
			Assert.That(data, Is.EqualTo(original));
		}

		[Test]
		public void AsyncRenderingAllocatesNeitherOnProducerNorConsumerAfterWarmup()
		{
			using var done = new AutoResetEvent(false);
			long consumerStart = 0, consumerAllocated = 0;
			var count = 0;
			using var observer = new BufferedFrameObserver<DmdFrame, DmdFrame>(f => f.ConvertGrayToRgb24(Palette), f => {
				if (++count == 100) consumerStart = GC.GetAllocatedBytesForCurrentThread();
				if (count == 1100) consumerAllocated = GC.GetAllocatedBytesForCurrentThread() - consumerStart;
				done.Set();
			}, false, 0, null, null);
			var input = new DmdFrame(Dim, new byte[Dim.Surface], 2);
			AssertNoAllocations(() => {
				input.Data[0] ^= 1;
				observer.OnNext(input);
				if (!done.WaitOne(5000)) throw new TimeoutException("Renderer did not consume the frame.");
			});
			Assert.That(consumerAllocated, Is.Zero, "consumer thread allocations");
		}

		[TestCase(SwitchMode.Replace, 2)]
		[TestCase(SwitchMode.ColorMask, 4)]
		[TestCase(SwitchMode.ColorMaskLoop, 4)]
		[TestCase(SwitchMode.FollowReplace, 2)]
		[TestCase(SwitchMode.LayeredColorMask, 4)]
		[TestCase(SwitchMode.MaskedReplace, 4)]
		[TestCase(SwitchMode.Replace, 15)]
		[TestCase(SwitchMode.Replace, 24)]
		public void VniAnimationRetriggerDoesNotAllocate(SwitchMode mode, int depth)
		{
			var input = new DmdFrame(Dim, new byte[Dim.Surface], 2);
			input.Data[0] = 1;
			var hash = FrameUtil.Checksum(input.BitPlanes[0]);
			var fsq = new FsqBuilder(2);
			var planes = Enumerable.Range(0, depth).Select(_ => Planes.With(3)).ToArray();
			var offset = fsq.Animation(new FsqFrame { Hash = hash, Planes = planes, Mask = mode == SwitchMode.MaskedReplace ? Planes.With(3) : null });
			var pal = new PalBuilder(1).Palette(0, 1, Palette).Mapping(hash, mode, 0, offset).BuildFile();
			using var colorizer = new ClockedColorizer(pal, new FsqFile(fsq.Build(), "allocation.fsq", pal));
			AssertNoAllocations(() => {
				input.Data[0] ^= 1;
				colorizer.Tick(input);
			});
		}

		[Test]
		public void ClockedInputSnapshotAndPaddingDoNotAllocate()
		{
			var pal = new PalBuilder(1).Palette(0, 1, Palette).BuildFile();
			using var colorizer = new ClockedColorizer(pal);
			var input = new DmdFrame(new Dimensions(128, 16), new byte[2048], 2);
			AssertNoAllocations(() => colorizer.Convert(input));
		}

		[Test]
		public void SwitchingFallbackDoesNotAllocate()
		{
			using var converter = new SwitchingConverter();
			var input = new DmdFrame(Dim, new byte[Dim.Surface], 2);
			AssertNoAllocations(() => converter.Convert(input));
		}

		[Test]
		public void Pin2DmdHdPackingDoesNotAllocate()
		{
			var device = (Pin2DmdHd)Activator.CreateInstance(typeof(Pin2DmdHd), true);
			var pack = (Func<Dimensions, byte[], byte[], int, int, int, bool>)typeof(Pin2DmdHd)
				.GetMethod("CreateRgb24HD", BindingFlags.NonPublic | BindingFlags.Instance)
				.CreateDelegate(typeof(Func<Dimensions, byte[], byte[], int, int, int, bool>), device);
			var frame = new byte[256 * 64 * 3];
			var output = new byte[256 * 64 * 3 + 4];
			AssertNoAllocations(() => { frame[0] ^= 255; pack(new Dimensions(256, 64), frame, output, 4, 0, 1); });
		}

		[Test]
		public void PixelcadeV1PackingDoesNotAllocate()
		{
			var device = (Pixelcade)Activator.CreateInstance(typeof(Pixelcade), true);
			typeof(Pixelcade).GetMethod("AllocateBuffers", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(device, null);
			var input = new DmdFrame(Dim, new byte[Dim.Surface * 3], 24);
			AssertNoAllocations(() => device.RenderRgb24(input));
		}

		[Test]
		public void SnapshotFormatChangesReusePreviouslySeenSizes()
		{
			var snapshot = new FrameSnapshot<DmdFrame>();
			var gray = new DmdFrame(Dim, new byte[4096], 2);
			var rgb = new DmdFrame(Dim * 2, new byte[16384 * 3], 24);
			var colored2 = new ColoredFrame(Dim, gray.Data, Palette);
			var colored6 = new ColoredFrame(Dim * 2, new byte[16384], new Color[64]);
			var raw = new RawFrame().Update(Dim, gray.Data, gray.BitPlanes, new[] { Planes.With(7) });
			AssertNoAllocations(() => {
				snapshot.Copy(gray);
				snapshot.Copy(rgb);
				snapshot.Copy(colored2);
				snapshot.Copy(colored6);
				snapshot.Copy(raw);
			});
		}

		[Test]
		public void VniRawAndPlainInputCanAlternateWithoutAllocations()
		{
			var pal = new PalBuilder(1).Palette(0, 1, Palette).BuildFile();
			using var colorizer = new ClockedColorizer(pal);
			var gray = new DmdFrame(Dim, new byte[4096], 2);
			var raw = new RawFrame().Update(Dim, gray.Data, gray.BitPlanes, new[] { Planes.With(7) });
			AssertNoAllocations(() => { colorizer.Tick(raw); colorizer.Tick(gray); });
		}

		private sealed class ClockedColorizer : VniColorizer
		{
			public ClockedColorizer(PalFile pal, AnimationSet animations = null) : base(pal, animations) { }
			public void Tick(DmdFrame frame) => ConvertClocked(frame);
		}

		private abstract class CountingDestination : IDestination
		{
			public int Count;
			public string Name => "Allocation test destination";
			public bool IsAvailable => true;
			public bool NeedsDuplicateFrames => true;
			public bool NeedsIdentificationFrames => false;
			public void ClearDisplay() { }
			public void Dispose() { }
		}
		private sealed class GrayDestination : CountingDestination, IGray2Destination
		{
			public void RenderGray2(DmdFrame frame) => Count++;
		}
		private sealed class RgbDestination : CountingDestination, IRgb24Destination
		{
			public void RenderRgb24(DmdFrame frame) => Count++;
			public void SetColor(Color color) { }
			public void ClearColor() { }
			public void SetPalette(Color[] palette) { }
			public void ClearPalette() { }
		}
	}
}
