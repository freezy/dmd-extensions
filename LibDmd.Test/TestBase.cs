using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Frame;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	public class TestBase
	{
		private TestContext testContextInstance;

		/// <summary>
		/// Gets or sets the test context which provides
		/// information about and functionality for the current test run.
		/// </summary>
		public TestContext TestContext
		{
			get { return testContextInstance; }
			set { testContextInstance = value; }
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

			receivedFrame.Planes.Length.Should().Be(expectedFrame.Planes.Length);
			for (var i = 0; i < receivedFrame.Planes.Length; i++) {
				receivedFrame.Planes[i].Should().BeEquivalentTo(expectedFrame.Planes[i]);
			}
			receivedFrame.Palette.Should().BeEquivalentTo(expectedFrame.Palette);
			receivedFrame.Dimensions.Should().Be(expectedFrame.Dimensions);
		}
	}
}
