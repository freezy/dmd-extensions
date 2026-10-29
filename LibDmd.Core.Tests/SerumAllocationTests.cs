using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using LibDmd.Converter.Serum;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Input;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class SerumAllocationTests
	{
		[Test]
		public void V1SkipsMissingNativePixelsWithoutPublishingFramesOrEvents()
		{
			using var frames = new Subject<ColoredFrame>();
			using var events = new Subject<FrameEvent>();
			var native = new SerumFrame { Width32 = 128, nocolors = 4, triggerID = 1 };
			var api = new SerumApiV1(frames, events, ref native);
			var publications = 0;
			using var frameSubscription = frames.Subscribe(_ => publications++);
			using var eventSubscription = events.Subscribe(_ => publications++);
			api.Convert(ref native);
			Assert.That(publications, Is.Zero);
		}

		[Test]
		public void V1KeepsTheLastPaletteWhenTheNativePaletteIsMissing()
		{
			var pixels = Marshal.AllocHGlobal(4096);
			var palette = Marshal.AllocHGlobal(192);
			try {
				Marshal.Copy(new byte[4096], 0, pixels, 4096);
				var colors = new byte[192]; colors[0] = 42;
				Marshal.Copy(colors, 0, palette, colors.Length);
				using var frames = new Subject<ColoredFrame>();
				using var events = new Subject<FrameEvent>();
				var native = new SerumFrame { Width32 = 128, nocolors = 4, Frame = pixels, Palette = palette, triggerID = uint.MaxValue };
				var api = new SerumApiV1(frames, events, ref native);
				Color observed = default;
				using var subscription = frames.Subscribe(f => observed = f.Palette[0]);
				api.Convert(ref native);
				native.Palette = IntPtr.Zero;
				observed = default;
				api.Convert(ref native);
				Assert.That(observed, Is.EqualTo(Color.FromRgb(42, 0, 0)));
				var rotated = new Color[64];
				api.UpdateRotations(ref native, rotated, 0);
				Assert.That(rotated[0], Is.EqualTo(observed));
			} finally { Marshal.FreeHGlobal(pixels); Marshal.FreeHGlobal(palette); }
		}

		[Test]
		public void ReadingNativeFrameMetadataDoesNotBoxTheStructure()
		{
			var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<SerumFrame>());
			try {
				Marshal.StructureToPtr(new SerumFrame { Width32 = 128 }, pointer, false);
				SerumFrame result = default;
				AllocationTests.AssertNoAllocations(() => result = SerumFrame.ReadFrom(pointer));
				Assert.That(result.Width32, Is.EqualTo(128));
			} finally { Marshal.FreeHGlobal(pointer); }
		}

		[Test]
		public void V1CopiesNativePixelsAndPaletteWithoutAllocations()
		{
			var pixels = Marshal.AllocHGlobal(4096);
			var palette = Marshal.AllocHGlobal(192);
			try {
				Marshal.Copy(new byte[4096], 0, pixels, 4096);
				Marshal.Copy(new byte[192], 0, palette, 192);
				using var frames = new Subject<ColoredFrame>();
				using var events = new Subject<FrameEvent>();
				var native = new SerumFrame { Width32 = 128, nocolors = 4, Frame = pixels, Palette = palette, triggerID = uint.MaxValue };
				var api = new SerumApiV1(frames, events, ref native);
				byte observed = 0;
				using var subscription = frames.Subscribe(f => observed = f.Data[0]);
				AllocationTests.AssertNoAllocations(() => {
					Marshal.WriteByte(pixels, (byte)(observed ^ 1));
					api.Convert(ref native);
				});
				Assert.That(observed, Is.Zero);
			} finally { Marshal.FreeHGlobal(pixels); Marshal.FreeHGlobal(palette); }
		}

		[Test]
		public void V2ReentrantPublicationDoesNotOverwriteTheOuterFrame()
		{
			var pixels = Marshal.AllocHGlobal(8192);
			try {
				Marshal.Copy(new byte[8192], 0, pixels, 8192);
				using var frames = new Subject<DmdFrame>();
				var native = new SerumFrame { Width32 = 128, Frame32Data = pixels };
				var api = new SerumApiV2(frames, ScalerMode.None, ref native);
				var reentered = false;
				byte before = 0, after = 0;
				using var subscription = frames.Subscribe(f => {
					if (reentered) return;
					before = f.Data[0];
					reentered = true;
					Marshal.WriteByte(pixels, 42);
					api.Convert(ref native);
					after = f.Data[0];
				});
				Marshal.WriteByte(pixels, 7);
				api.Convert(ref native);
				Assert.That(before, Is.EqualTo(7));
				Assert.That(after, Is.EqualTo(7));
				AllocationTests.AssertNoAllocations(() => api.Convert(ref native));
			} finally { Marshal.FreeHGlobal(pixels); }
		}
	}
}
