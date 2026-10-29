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
