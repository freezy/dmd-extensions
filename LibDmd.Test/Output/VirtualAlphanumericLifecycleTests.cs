using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Output.Virtual.AlphaNumeric;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, SingleThreaded]
	public class VirtualAlphanumericLifecycleTests
	{
		private VirtualAlphanumericDestination _destination;
		private Application _application;

		[OneTimeSetUp]
		public void StartApplication()
		{
			_application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
		}

		[SetUp]
		public void SetUp()
		{
			_destination = VirtualAlphanumericDestination.GetInstance(Dispatcher.CurrentDispatcher, new RasterizeStyleDefinition(), null);
		}

		[TearDown]
		public void TearDown() => _destination.Dispose();

		[OneTimeTearDown]
		public void StopDispatcher() => _application.Shutdown();

		private static AlphaNumericFrame Frame(NumericalLayout layout) => new AlphaNumericFrame(layout, new ushort[40], new ushort[6]);

		private VirtualAlphaNumericDisplay[] Displays() => ((Dictionary<int, VirtualAlphaNumericDisplay>)typeof(VirtualAlphanumericDestination)
			.GetField("_displays", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_destination)).Values.ToArray();

		[Test]
		public void ShouldReplaceLayoutBeforePumpingDispatcher()
		{
			_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x16Alpha));
			var oldDisplays = Displays();
			var closed = 0;
			foreach (var display in oldDisplays) {
				display.Closed += (s, e) => closed++;
			}

			// No dispatcher pumping between frames: previously Close hit a worker
			// that had not yet created its dispatcher, with Init/Show still queued.
			_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x7Alpha_2x7Num));

			closed.Should().Be(2);
			oldDisplays.Should().OnlyContain(d => !d.IsVisible);
			Displays().Should().HaveCount(4).And.OnlyContain(d => d.IsVisible);
			Dispatcher.CurrentDispatcher.HasShutdownStarted.Should().BeFalse();
		}

		[Test]
		public void ShouldKeepApplicationWindowsOpenDuringLayoutReplacement()
		{
			_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x16Alpha));
			var minimumWindows = int.MaxValue;
			foreach (var display in Displays()) {
				display.Closed += (s, e) => minimumWindows = Math.Min(minimumWindows, _application.Windows.Count);
			}

			_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x7Alpha_2x7Num));

			// With the console host's OnLastWindowClose policy, reaching zero here
			// queues Application.Shutdown, even if replacement windows open later.
			minimumWindows.Should().BeGreaterThan(0);
			_application.Windows.Count.Should().Be(4);
		}

		[Test]
		public void ShouldDisposeImmediatelyAndIgnoreLateFrames()
		{
			_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x16Alpha));
			var oldDisplays = Displays();
			_destination.Dispose();
			_destination.Dispose();
			_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x7Alpha_2x7Num));

			Displays().Should().BeEmpty();
			oldDisplays.Should().OnlyContain(d => !d.IsVisible);
		}

		[Test]
		public void ShouldSerializeWorkerFramesAndShutdownOnOwnerDispatcher()
		{
			var task = Task.Run(() => {
				_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x16Alpha));
				_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x7Alpha_2x7Num));
				_destination.ClearDisplay();
				_destination.Dispose();
			});
			var pump = new DispatcherFrame();
			var deadline = DateTime.UtcNow.AddSeconds(10);
			var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
			timer.Tick += (s, e) => {
				if (task.IsCompleted || DateTime.UtcNow >= deadline) {
					pump.Continue = false;
				}
			};
			timer.Start();
			try {
				Dispatcher.PushFrame(pump);
			} finally {
				timer.Stop();
			}
			task.IsCompleted.Should().BeTrue("dispatcher operations must complete without deadlocking");
			task.GetAwaiter().GetResult();
			Displays().Should().BeEmpty();
		}

		[Test]
		public void ShouldReallyCloseSettingsDuringDestinationDisposal()
		{
			_destination.RenderAlphaNumeric(Frame(NumericalLayout.__2x16Alpha));
			typeof(VirtualAlphanumericDestination).GetMethod("ToggleSettings", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(_destination, new object[] { 0 });
			var settings = (VirtualAlphaNumericSettings)typeof(VirtualAlphanumericDestination)
				.GetField("_settingWindow", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_destination);
			var closed = false;
			settings.Closed += (s, e) => closed = true;
			settings.Close();
			closed.Should().BeFalse("normal user close should only hide settings");
			settings.Show();

			_destination.Dispose();

			closed.Should().BeTrue("disposing the destination must actually close settings");
			settings.IsVisible.Should().BeFalse();
		}

		[Test]
		public void ShouldRouteAllTenAuxiliaryDigitsAndClearEveryDisplay()
		{
			var segments = Enumerable.Range(1, 32).Select(i => (ushort)i).ToArray();
			var extended = Enumerable.Range(33, 6).Select(i => (ushort)i).ToArray();
			_destination.RenderAlphaNumeric(new AlphaNumericFrame(NumericalLayout.__2x7Num_2x7Num_10x1Num, segments, extended));
			var displays = Displays();
			var auxiliary = displays.Single(d => d.AlphaNumericDisplay.DisplaySetting.NumChars == 10);
			SegmentData(auxiliary.AlphaNumericDisplay).Should().Equal(segments.Skip(28).Concat(extended));

			_destination.ClearDisplay();

			foreach (var display in displays) {
				var control = display.AlphaNumericDisplay;
				SegmentData(control).Should().HaveCount(control.DisplaySetting.NumChars * control.DisplaySetting.NumLines)
					.And.OnlyContain(value => value == 0);
			}
		}

		[Test]
		public void ShouldClearEveryCharacterInAMultilineDisplay()
		{
			var control = new AlphanumericControl {
				DisplaySetting = new DisplaySetting { NumChars = 7, NumLines = 2 }
			};
			try {
				control.RenderSegments(Enumerable.Repeat((ushort)1, 14).ToArray());
				control.ClearDisplay();
				SegmentData(control).Should().HaveCount(14).And.OnlyContain(value => value == 0);
				// Rendering after a clear must still be able to inspect every previous digit.
				control.RenderSegments(Enumerable.Repeat((ushort)2, 14).ToArray());
			} finally {
				control.Dispose();
			}
		}

		private static ushort[] SegmentData(AlphanumericControl control) => (ushort[])typeof(AlphanumericControl)
			.GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);

		[Test]
		public void ShouldReleaseDisposedControlFromRenderingEvent()
		{
			var reference = DisposedControl();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			reference.IsAlive.Should().BeFalse("the static rendering event must not retain disposed controls");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static WeakReference DisposedControl()
		{
			var control = new AlphanumericControl();
			control.Dispose();
			return new WeakReference(control);
		}
	}
}
