using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Frame;
using LibDmd.Test.Stubs;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace LibDmd.Test
{
	public class TestBase
	{
		private TestContext testContextInstance;

		private TestLogger _testLogger = new TestLogger();

		/// <summary>
		/// Gets or sets the test context which provides
		/// information about and functionality for the current test run.
		/// </summary>
		public TestContext TestContext
		{
			get { return testContextInstance; }
			set { testContextInstance = value; }
		}

		protected void AddLogger()
		{
			if (LogManager.Configuration == null) {
				LogManager.Configuration = new NLog.Config.LoggingConfiguration();
			}
			LogManager.Configuration.AddTarget("test", _testLogger);
			LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, _testLogger));
			LogManager.ReconfigExistingLoggers();
		}

		protected static void RemoveLogger()
		{
			LogManager.Configuration.RemoveTarget("test");
			LogManager.Configuration.LoggingRules.Clear();
		}

		protected static void Print(object obj, string label = "")
		{
			TestContext.WriteLine(label + obj);
		}

		protected static async Task AssertFrame<TFrame>(ITestSource<TFrame> source, ITestDestination<BmpFrame> dest, TFrame srcFrame, DmdFrame expectedFrame)
		{
			Print(srcFrame, "In: ");

			dest.Reset();
			source.AddFrame(srcFrame);
			var receivedFrame = await dest.Frame;
			var receivedFrameRgb = receivedFrame.ConvertToRgb24();

			Print(receivedFrameRgb, "Out: ");

			receivedFrameRgb.Data.Should().BeEquivalentTo(expectedFrame.Data);
			receivedFrameRgb.Dimensions.Should().Be(expectedFrame.Dimensions);
		}

		protected static async Task AssertFrame<TFrame>(ITestSource<TFrame> source, ITestDestination<BmpFrame> dest, TFrame srcFrame, BmpFrame expectedFrame)
		{
			Print(srcFrame, "In: ");

			dest.Reset();
			source.AddFrame(srcFrame);
			var receivedFrame = await dest.Frame;
			var receivedFrameRgb = receivedFrame.ConvertToRgb24();

			Print(receivedFrameRgb, "Out: ");

			receivedFrameRgb.Data.Should().BeEquivalentTo(expectedFrame.ConvertToRgb24().Data);
			receivedFrameRgb.Dimensions.Should().Be(expectedFrame.Dimensions);
		}

		protected static async Task AssertFrame<TFrame>(ITestSource<TFrame> source, ITestDestination<DmdFrame> dest, TFrame srcFrame, DmdFrame expectedFrame)
		{
			Print(srcFrame, "In: ");
			
			dest.Reset();
			source.AddFrame(srcFrame);
			var receivedFrame = await dest.Frame;
			
			Print(receivedFrame, "Out: ");
			
			receivedFrame.Data.Should().BeEquivalentTo(expectedFrame.Data);
			receivedFrame.BitLength.Should().Be(expectedFrame.BitLength);
			receivedFrame.Dimensions.Should().Be(expectedFrame.Dimensions);
		}

		protected static async Task AssertFrame<TFrame>(ITestSource<TFrame> source, ITestDestination<ColoredFrame> dest, TFrame srcFrame, ColoredFrame expectedFrame)
		{
			Print(srcFrame, "In: ");

			dest.Reset();
			source.AddFrame(srcFrame);
			var receivedFrame = await dest.Frame;

			Print(receivedFrame, "Out: ");

			Print(expectedFrame, "Expected: ");

			receivedFrame.Data.Length.Should().Be(expectedFrame.Data.Length);
			receivedFrame.Data.Should().BeEquivalentTo(expectedFrame.Data);
			receivedFrame.Palette.Should().BeEquivalentTo(expectedFrame.Palette);
			receivedFrame.Dimensions.Should().Be(expectedFrame.Dimensions);
		}

		class TestLogger : Target {
			protected override void Write(LogEventInfo logEvent) => TestContext.WriteLine($"[{logEvent.TimeStamp.Hour}:{logEvent.TimeStamp.Minute}:{logEvent.TimeStamp.Second}.{logEvent.TimeStamp.Millisecond}] {logEvent.FormattedMessage}");
		}
	}
}
