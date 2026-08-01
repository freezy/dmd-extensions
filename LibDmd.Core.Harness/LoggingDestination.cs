using System;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Core.Harness
{
	/// <summary>
	/// A trivial fixed-size gray2 destination that logs the frames it receives, so the
	/// harness can prove frames flow through the RenderGraph without any real hardware.
	/// </summary>
	internal class LoggingDestination : IGray2Destination, IFixedSizeDestination
	{
		public string Name => "Console Logger";
		public bool IsAvailable => true;
		public bool NeedsDuplicateFrames => false;
		public bool NeedsIdentificationFrames => false;
		public Dimensions FixedSize => Dimensions.Standard;
		public bool DmdAllowHdScaling => false;

		public int FrameCount { get; private set; }

		public void RenderGray2(DmdFrame frame)
		{
			FrameCount++;
			long checksum = 0;
			foreach (var b in frame.Data) {
				checksum += b;
			}
			Console.WriteLine($"    [dest] gray2 frame #{FrameCount}: {frame.Dimensions}, {frame.Data.Length} bytes, checksum={checksum}");
		}

		public void ClearDisplay() => Console.WriteLine("    [dest] ClearDisplay()");

		public void Dispose() { }
	}
}
