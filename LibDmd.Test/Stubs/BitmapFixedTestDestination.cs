using System;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class BitmapFixedTestDestination : FixedTestDestination<BmpFrame>, IBitmapDestination
	{
		public string Name => "Test Destination (Fixed Bitmap)";
		public bool IsAvailable => true;

		public BitmapFixedTestDestination(int dmdWidth, int dmdHeight, bool dmdAllowHdScaling = true) : base(dmdWidth, dmdHeight, dmdAllowHdScaling)
		{
		}

		public void RenderBitmap(BmpFrame frame)
		{
			LastFrame.OnNext(frame);
		}

		public void Dispose()
		{
		}
	}
}
