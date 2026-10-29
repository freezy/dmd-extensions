using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibDmd.Common;
using LibDmd.Frame;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class BufferedDeliveryTests
	{
		[Test]
		public void SlowConsumerPreservesFifoAndReusesTheBacklogStorage()
		{
			using var release = new ManualResetEvent(false);
			using var drained = new AutoResetEvent(false);
			var received = new List<byte>(96);
			using var observer = new BufferedFrameObserver<DmdFrame, DmdFrame>(f => f, f => {
				release.WaitOne(5000);
				received.Add(f.Data[0]);
				if (received.Count == 64 || received.Count == 96) drained.Set();
			}, false, 0, null, null);
			var input = new DmdFrame(new Dimensions(8, 1), new byte[8], 8);
			try {
				for (byte i = 0; i < 64; i++) { input.Data[0] = i; observer.OnNext(input); }
				release.Set();
				Assert.That(drained.WaitOne(5000), Is.True);
				release.Reset();
				var start = GC.GetAllocatedBytesForCurrentThread();
				for (byte i = 64; i < 96; i++) { input.Data[0] = i; observer.OnNext(input); }
				var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
				input.Data[0] = 255;
				release.Set();
				Assert.That(drained.WaitOne(5000), Is.True);
				Assert.That(allocated, Is.Zero);
				Assert.That(received, Is.EqualTo(Enumerable.Range(0, 96).Select(i => (byte)i)));
			} finally { release.Set(); }
		}

		[Test]
		public void AsyncRenderingContinuesAfterOneDestinationFailure()
		{
			using var received = new AutoResetEvent(false);
			var calls = 0;
			using var observer = new BufferedFrameObserver<DmdFrame, DmdFrame>(f => f, f => {
				if (++calls == 1) throw new InvalidOperationException("Test destination failure");
				received.Set();
			}, false, 0, null, null);
			var input = new DmdFrame(new Dimensions(8, 1), new byte[8], 2);
			observer.OnNext(input);
			observer.OnNext(input);
			observer.OnCompleted();
			Assert.That(received.WaitOne(5000), Is.True);
			Assert.That(calls, Is.EqualTo(2));
		}

		[Test]
		public void DisposalInsideAWorkerCallbackDoesNotDeadlock()
		{
			using var disposed = new AutoResetEvent(false);
			BufferedFrameObserver<DmdFrame, DmdFrame> observer = null;
			observer = new BufferedFrameObserver<DmdFrame, DmdFrame>(f => f, f => { observer.Dispose(); disposed.Set(); }, false, 0, null, null);
			using (observer) {
				observer.OnNext(new DmdFrame(new Dimensions(8, 1), new byte[8], 2));
				Assert.That(disposed.WaitOne(5000), Is.True);
			}
		}

		[Test]
		public void IdleCannotClearTheDisplayWhileAFrameIsBeingRendered()
		{
			using var rendered = new AutoResetEvent(false);
			using var entered = new AutoResetEvent(false);
			using var release = new ManualResetEvent(false);
			using var idle = new AutoResetEvent(false);
			var count = 0;
			using var observer = new BufferedFrameObserver<DmdFrame, DmdFrame>(f => f, f => {
				if (++count == 2) {
					// A slow scheduler may legitimately idle between the two frames.
					// Only clearing during this render would violate the contract.
					idle.Reset();
					entered.Set();
					release.WaitOne(5000);
				}
				rendered.Set();
			}, false, 50, () => { }, () => idle.Set());
			var input = new DmdFrame(new Dimensions(8, 1), new byte[8], 2);
			try {
				observer.OnNext(input);
				Assert.That(rendered.WaitOne(5000), Is.True);
				observer.OnNext(input);
				Assert.That(entered.WaitOne(5000), Is.True);
				// A previous frame's idle callback must wait for the current render and recheck its deadline.
				Assert.That(idle.WaitOne(100), Is.False);
				release.Set();
				Assert.That(rendered.WaitOne(5000), Is.True);
				Assert.That(idle.WaitOne(5000), Is.True);
			} finally { release.Set(); }
		}

		[Test]
		public void CompletedSynchronousObserverIgnoresFurtherInput()
		{
			var count = 0;
			using var observer = new BufferedFrameObserver<DmdFrame, DmdFrame>(f => f, _ => count++, true, 0, null, null);
			var input = new DmdFrame(new Dimensions(8, 1), new byte[8], 2);
			observer.OnNext(input);
			observer.OnCompleted();
			observer.OnNext(input);
			Assert.That(count, Is.EqualTo(1));
		}
	}
}
